using DanTaskManager.Application.Tasks.CreateTask;
using DanTaskManager.Application.Tasks.GetTaskById;
using DanTaskManager.Contracts.Requests.Tasks;
using DanTaskManager.Controllers;
using DanTaskManager.Domain;
using DanTaskManager.Services;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;

namespace DanTaskManager.Tests;

public class TasksControllerErrorContractTests
{
    [Fact]
    public async Task CreateTask_WhenApplicationRejectsTaskType_ThrowsValidationExceptionWithStableCode()
    {
        using var customFields = JsonDocument.Parse("{\"priority\":\"high\"}");
        var request = new CreateTaskRequest
        {
            TaskType = "Bug",
            Description = "Fix production error",
            AssignedToUserId = 17,
            CustomFields = customFields.RootElement
        };
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(
                It.IsAny<IRequest<TaskCreationResult>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaskCreationResult.FailureResult(
                "TaskType 'Bug' is not supported",
                new[] { "Development", "Procurement" }));

        var controller = CreateController(mediator);

        var exception = await Assert.ThrowsAsync<ApiValidationException>(() => controller.CreateTask(request));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("task_type_validation_failed", exception.Code);
        Assert.Contains("TaskType 'Bug' is not supported", exception.Message);
        Assert.Contains("Development, Procurement", exception.Message);
        mediator.Verify(m => m.Send(
                It.Is<IRequest<TaskCreationResult>>(command =>
                    command is CreateTaskCommand createTaskCommand &&
                    createTaskCommand.TaskType == request.TaskType &&
                    createTaskCommand.Description == request.Description &&
                    createTaskCommand.AssignedToUserId == request.AssignedToUserId &&
                    createTaskCommand.CustomDataJson == "{\"priority\":\"high\"}"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateTask_WhenRequestValidationFails_ThrowsValidationExceptionWithoutCallingMediator()
    {
        var mediator = new Mock<IMediator>();
        var validator = new Mock<IValidator<CreateTaskRequest>>();
        validator
            .Setup(v => v.ValidateAsync(
                It.IsAny<CreateTaskRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure(nameof(CreateTaskRequest.Description), "Description is required")
            }));
        var controller = CreateController(mediator, validator.Object);

        var exception = await Assert.ThrowsAsync<ApiValidationException>(() => controller.CreateTask(new CreateTaskRequest()));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("validation_failed", exception.Code);
        Assert.Equal("Description is required", exception.Message);
        mediator.Verify(m => m.Send(
                It.IsAny<IRequest<TaskCreationResult>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetTask_WhenTaskDoesNotExist_ThrowsNotFoundExceptionWithStableCode()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(
                It.IsAny<IRequest<TaskDetailsDto?>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskDetailsDto?)null);
        var controller = CreateController(mediator);

        var exception = await Assert.ThrowsAsync<ApiNotFoundException>(() => controller.GetTask(404));

        Assert.Equal(StatusCodes.Status404NotFound, exception.StatusCode);
        Assert.Equal("not_found", exception.Code);
        mediator.Verify(m => m.Send(
                It.Is<IRequest<TaskDetailsDto?>>(query =>
                    query is GetTaskByIdQuery getTaskByIdQuery &&
                    getTaskByIdQuery.TaskId == 404),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static TasksController CreateController(
        Mock<IMediator> mediator,
        IValidator<CreateTaskRequest>? createTaskValidator = null)
    {
        return new TasksController(
            createTaskValidator ?? PassingValidator<CreateTaskRequest>(),
            PassingValidator<ChangeStatusWorkflowRequest>(),
            PassingValidator<CloseTaskRequest>(),
            mediator.Object,
            NullLogger<TasksController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static IValidator<T> PassingValidator<T>()
    {
        var validator = new Mock<IValidator<T>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        return validator.Object;
    }
}

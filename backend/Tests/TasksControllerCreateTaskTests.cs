using DanTaskManager.Application.Tasks.CreateTask;
using DanTaskManager.Controllers;
using DanTaskManager.Services;
using DanTaskManager.Validation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using Xunit;

namespace DanTaskManager.Tests;

public class TasksControllerCreateTaskTests
{
    private readonly Mock<ITaskApplicationService> _taskService = new();
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task CreateTask_WithValidRequest_SendsMediatorCommandAndReturnsCreatedTask()
    {
        // Arrange
        using var requestAbortedSource = new CancellationTokenSource();
        using var customFieldsDocument = JsonDocument.Parse("""{"priority":"high","estimate":3}""");
        var expectedTask = new TaskDetailsDto
        {
            Id = 42,
            TaskType = "Development",
            Description = "Implement MediatR migration",
            AssignedToUserId = 7,
            CustomFields = customFieldsDocument.RootElement.Clone()
        };

        _mediator
            .Setup(mediator => mediator.Send(
                It.Is<CreateTaskCommand>(command =>
                    command.TaskType == "Development" &&
                    command.Description == "Implement MediatR migration" &&
                    command.AssignedToUserId == 7 &&
                    command.CustomDataJson == """{"priority":"high","estimate":3}"""),
                requestAbortedSource.Token))
            .ReturnsAsync(TaskCreationResult.SuccessResult(expectedTask));

        var controller = CreateController(requestAbortedSource.Token);
        var request = new CreateTaskRequest
        {
            TaskType = "Development",
            Description = "Implement MediatR migration",
            AssignedToUserId = 7,
            CustomFields = customFieldsDocument.RootElement.Clone()
        };

        // Act
        var response = await controller.CreateTask(request);

        // Assert
        var createdAt = Assert.IsType<CreatedAtActionResult>(response.Result);
        Assert.Equal(nameof(TasksController.GetTask), createdAt.ActionName);
        Assert.Equal(42, createdAt.RouteValues!["id"]);
        Assert.Same(expectedTask, createdAt.Value);

        _mediator.Verify(mediator => mediator.Send(
                It.IsAny<CreateTaskCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _taskService.Verify(service => service.CreateAsync(
                It.IsAny<TaskCreateCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateTask_WhenMediatorReturnsSupportedTaskTypes_ReturnsClientErrorPayload()
    {
        // Arrange
        const string errorMessage = "סוג משימה לא נתמך: Unknown";
        var supportedTaskTypes = new[] { "Analysis", "Development" };

        _mediator
            .Setup(mediator => mediator.Send(
                It.IsAny<CreateTaskCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaskCreationResult.FailureResult(errorMessage, supportedTaskTypes));

        var controller = CreateController(CancellationToken.None);
        var request = new CreateTaskRequest
        {
            TaskType = "Unknown",
            Description = "Unsupported task type",
            AssignedToUserId = 7
        };

        // Act
        var response = await controller.CreateTask(request);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        var payload = JsonSerializer.SerializeToElement(badRequest.Value);

        Assert.Equal(errorMessage, payload.GetProperty("error").GetString());
        Assert.Equal(
            supportedTaskTypes,
            payload.GetProperty("supportedTaskTypes").EnumerateArray().Select(type => type.GetString()!).ToArray());
    }

    private TasksController CreateController(CancellationToken requestAborted)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestAborted = requestAborted
        };

        return new TasksController(
            _taskService.Object,
            new CreateTaskRequestValidator(),
            Mock.Of<FluentValidation.IValidator<ChangeStatusWorkflowRequest>>(),
            Mock.Of<FluentValidation.IValidator<CloseTaskRequest>>(),
            _mediator.Object,
            NullLogger<TasksController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }
}

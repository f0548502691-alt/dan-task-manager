using DanTaskManager.Controllers;
using DanTaskManager.Domain;
using DanTaskManager.Services;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace DanTaskManager.Tests;

public class TasksControllerTests
{
    [Fact]
    public async Task CreateTask_WhenServiceReturnsSupportedTaskTypes_IncludesThemInBadRequest()
    {
        var service = new StubTaskApplicationService(
            TaskCreationResult.FailureResult(
                "סוג משימה לא נתמך: UnknownType",
                new[] { "Development", "Procurement" }));
        var controller = CreateController(service);

        var response = await controller.CreateTask(new CreateTaskRequest
        {
            TaskType = "UnknownType",
            Description = "Unsupported task type",
            AssignedToUserId = 1,
            CustomDataJson = "{}"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        using var json = JsonSerializer.SerializeToDocument(badRequest.Value);
        var root = json.RootElement;

        Assert.Equal("סוג משימה לא נתמך: UnknownType", root.GetProperty("error").GetString());
        Assert.Equal(
            new[] { "Development", "Procurement" },
            root.GetProperty("supportedTaskTypes").EnumerateArray().Select(type => type.GetString()).ToArray());
    }

    [Fact]
    public async Task CreateTask_WhenServiceFailureHasNoSupportedTaskTypes_OmitsSupportedTaskTypes()
    {
        var service = new StubTaskApplicationService(TaskCreationResult.FailureResult("משתמש לא קיים"));
        var controller = CreateController(service);

        var response = await controller.CreateTask(new CreateTaskRequest
        {
            TaskType = "Procurement",
            Description = "Missing user",
            AssignedToUserId = 999,
            CustomDataJson = "{}"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        using var json = JsonSerializer.SerializeToDocument(badRequest.Value);
        var root = json.RootElement;

        Assert.Equal("משתמש לא קיים", root.GetProperty("error").GetString());
        Assert.False(root.TryGetProperty("supportedTaskTypes", out _));
    }

    private static TasksController CreateController(ITaskApplicationService taskService)
    {
        var controller = new TasksController(
            taskService,
            new InlineValidator<CreateTaskRequest>(),
            new InlineValidator<ChangeStatusWorkflowRequest>(),
            new InlineValidator<CloseTaskRequest>(),
            new NullLogger<TasksController>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private class StubTaskApplicationService : ITaskApplicationService
    {
        private readonly TaskCreationResult _createResult;

        public StubTaskApplicationService(TaskCreationResult createResult)
        {
            _createResult = createResult;
        }

        public Task<PagedResult<TaskSummaryDto>> GetAllAsync(
            PageRequest pageRequest,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<PagedResult<TaskSummaryDto>> GetByTypeAsync(
            string taskType,
            PageRequest pageRequest,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<PagedResult<TaskSummaryDto>> GetByUserAsync(
            int userId,
            PageRequest pageRequest,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<TaskDetailsDto?> GetByIdAsync(int taskId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> UserExistsAsync(int userId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<TaskCreationResult> CreateAsync(
            TaskCreateCommand command,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_createResult);

        public Task<bool> UpdateDescriptionAsync(
            int taskId,
            string? description,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> DeleteAsync(int taskId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<WorkflowResult> ChangeStatusAsync(
            int taskId,
            int newStatus,
            int nextAssignedToUserId,
            string newDataJson,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<WorkflowResult> CloseAsync(
            int taskId,
            string finalNotes,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}

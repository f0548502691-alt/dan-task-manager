using DanTaskManager.Data;
using DanTaskManager.Domain.Handlers;
using DanTaskManager.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace DanTaskManager.Tests;

/// <summary>
/// Verifies that <see cref="WorkflowResult.Code"/> exposes a stable machine-
/// readable code on every failure path of <see cref="TaskWorkflowService"/>.
/// </summary>
public class WorkflowResultErrorCodeTests : IAsyncLifetime
{
    private ApplicationDbContext _context = null!;
    private TaskWorkflowService _service = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ErrorCodes-{Guid.NewGuid()}")
            .Options;
        _context = new ApplicationDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        var validation = new TaskTypeValidationService(_context, new MemoryCache(new MemoryCacheOptions()));
        var providers = new ITaskWorkflowRuleProvider[]
        {
            new MetadataTaskWorkflowRuleProvider(validation),
            new HandlerTaskWorkflowRuleProvider(new TaskHandlerFactory(Array.Empty<ITaskHandler>()))
        };
        _service = new TaskWorkflowService(_context, providers, NullLogger<TaskWorkflowService>.Instance);
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task UnknownTaskId_ReturnsTaskNotFoundCode()
    {
        var result = await _service.ChangeStatusAsync(999999, 2, 1, "{}");

        Assert.False(result.Success);
        Assert.Equal(WorkflowErrorCodes.TaskNotFound, result.Code);
    }

    [Fact]
    public async Task UnknownAssignee_ReturnsAssigneeNotFoundCode()
    {
        var task = await CreateProcurementTaskAsync();

        var result = await _service.ChangeStatusAsync(task.Id, 2, 999, "{\"prices\":[\"1\",\"2\"]}");

        Assert.False(result.Success);
        Assert.Equal(WorkflowErrorCodes.AssigneeNotFound, result.Code);
    }

    [Fact]
    public async Task MalformedCustomDataJson_ReturnsInvalidJsonCode()
    {
        var task = await CreateProcurementTaskAsync();

        var result = await _service.ChangeStatusAsync(task.Id, 2, 1, "not-json");

        Assert.False(result.Success);
        Assert.Equal(WorkflowErrorCodes.InvalidJsonPayload, result.Code);
    }

    [Fact]
    public async Task ForwardSkip_ReturnsIllegalStatusTransitionCode()
    {
        var task = await CreateProcurementTaskAsync();

        var result = await _service.ChangeStatusAsync(task.Id, 3, 1, "{\"receipt\":\"r\"}");

        Assert.False(result.Success);
        Assert.Equal(WorkflowErrorCodes.IllegalStatusTransition, result.Code);
    }

    [Fact]
    public async Task DirectCloseStatus_ReturnsCloseViaCloseTaskOnlyCode()
    {
        var task = await CreateProcurementTaskAsync();

        var result = await _service.ChangeStatusAsync(task.Id, 99, 1, "{}");

        Assert.False(result.Success);
        Assert.Equal(WorkflowErrorCodes.CloseViaCloseTaskOnly, result.Code);
    }

    [Fact]
    public async Task SameStatus_ReturnsSameStatusCode()
    {
        var task = await CreateProcurementTaskAsync();

        var result = await _service.ChangeStatusAsync(task.Id, 1, 1, "{}");

        Assert.False(result.Success);
        Assert.Equal(WorkflowErrorCodes.SameStatus, result.Code);
    }

    [Fact]
    public async Task FieldValidationFails_ReturnsFieldValidationFailedCode()
    {
        var task = await CreateProcurementTaskAsync();

        var result = await _service.ChangeStatusAsync(task.Id, 2, 1, "{}");

        Assert.False(result.Success);
        Assert.Equal(WorkflowErrorCodes.FieldValidationFailed, result.Code);
    }

    [Fact]
    public async Task CloseBeforeFinalStatus_ReturnsCloseRequiresFinalStatusCode()
    {
        var task = await CreateProcurementTaskAsync();

        var result = await _service.CloseTaskAsync(task.Id, 1, "premature");

        Assert.False(result.Success);
        Assert.Equal(WorkflowErrorCodes.CloseRequiresFinalStatus, result.Code);
    }

    [Fact]
    public async Task CloseWithInvalidFinalStatusPayload_ReturnsFieldValidationFailedCode()
    {
        var task = await CreateProcurementTaskAsync();
        task.CurrentStatus = 3;
        task.CustomDataJson = "{}";
        await _context.SaveChangesAsync();

        var result = await _service.CloseTaskAsync(task.Id, 1, "invalid close payload");

        Assert.False(result.Success);
        Assert.Equal(WorkflowErrorCodes.FieldValidationFailed, result.Code);
    }

    [Fact]
    public async Task UnsupportedTaskType_ReturnsUnsupportedTaskTypeCode()
    {
        var task = new Domain.BaseTask
        {
            TaskType = "ThisTypeDoesNotExist",
            Description = "x",
            CurrentStatus = 1,
            AssignedToUserId = 1,
            CustomDataJson = "{}"
        };
        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        var result = await _service.ChangeStatusAsync(task.Id, 2, 1, "{}");

        Assert.False(result.Success);
        Assert.Equal(WorkflowErrorCodes.UnsupportedTaskType, result.Code);
    }

    [Fact]
    public async Task SuccessfulTransition_HasEmptyCode()
    {
        var task = await CreateProcurementTaskAsync();

        var result = await _service.ChangeStatusAsync(task.Id, 2, 1,
            "{\"prices\":[\"1.50\",\"2.00\"]}");

        Assert.True(result.Success, result.Message);
        Assert.Equal(string.Empty, result.Code);
    }

    private async Task<Domain.BaseTask> CreateProcurementTaskAsync()
    {
        var task = new Domain.BaseTask
        {
            TaskType = "Procurement",
            Description = "Test",
            CurrentStatus = 1,
            AssignedToUserId = 1,
            CustomDataJson = "{}"
        };
        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }
}

using DanTaskManager.Data;
using DanTaskManager.Domain.Handlers;
using DanTaskManager.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace DanTaskManager.Tests;

/// <summary>
/// Demonstrates the "add a third task type without touching existing code"
/// requirement. Marketing exists only as seeded metadata rows; no
/// MarketingTaskHandler class is registered. The workflow service still
/// validates and advances Marketing tasks correctly.
/// </summary>
public class MarketingTaskTypeIntegrationTests : IAsyncLifetime
{
    private ApplicationDbContext _context = null!;
    private TaskWorkflowService _service = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"MarketingType-{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        var validationService = new TaskTypeValidationService(_context, new MemoryCache(new MemoryCacheOptions()));

        var providers = new ITaskWorkflowRuleProvider[]
        {
            new MetadataTaskWorkflowRuleProvider(validationService),
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
    public async Task Marketing_TaskType_IsServedByMetadataProvider()
    {
        var task = await CreateMarketingTaskAsync();

        var result = await _service.ChangeStatusAsync(
            task.Id,
            newStatus: 2,
            nextAssignedToUserId: 1,
            newDataJson: "{\"campaignName\":\"Spring Launch\",\"targetAudience\":\"B2C\"}");

        Assert.True(result.Success, result.Message);
        Assert.Equal(2, result.NewStatus);
    }

    [Fact]
    public async Task Marketing_Status2_RejectsMissingCampaignName()
    {
        var task = await CreateMarketingTaskAsync();

        var result = await _service.ChangeStatusAsync(
            task.Id,
            newStatus: 2,
            nextAssignedToUserId: 1,
            newDataJson: "{\"targetAudience\":\"B2C\"}");

        Assert.False(result.Success);
        Assert.Contains("campaignName", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Marketing_Status2_RejectsTargetAudienceOutsideAllowedValues()
    {
        var task = await CreateMarketingTaskAsync();

        var result = await _service.ChangeStatusAsync(
            task.Id,
            newStatus: 2,
            nextAssignedToUserId: 1,
            newDataJson: "{\"campaignName\":\"Spring\",\"targetAudience\":\"Internal2\"}");

        Assert.False(result.Success);
        Assert.Contains("allowed values", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Marketing_Status3_RejectsMalformedLaunchDate()
    {
        var task = await CreateMarketingTaskAsync();
        task.CurrentStatus = 2;
        task.CustomDataJson = "{\"campaignName\":\"Spring\",\"targetAudience\":\"B2C\"}";
        await _context.SaveChangesAsync();

        var result = await _service.ChangeStatusAsync(
            task.Id,
            newStatus: 3,
            nextAssignedToUserId: 1,
            newDataJson: "{\"launchDate\":\"15/04/2026\"}");

        Assert.False(result.Success);
        Assert.Contains("launchDate", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Marketing_CompleteHappyPath_AdvancesToFinalAndCloses()
    {
        var task = await CreateMarketingTaskAsync();

        var s2 = await _service.ChangeStatusAsync(task.Id, 2, 1,
            "{\"campaignName\":\"Spring Launch\",\"targetAudience\":\"B2C\"}");
        Assert.True(s2.Success, s2.Message);

        var s3 = await _service.ChangeStatusAsync(task.Id, 3, 1,
            "{\"campaignName\":\"Spring Launch\",\"targetAudience\":\"B2C\",\"launchDate\":\"2026-04-15\"}");
        Assert.True(s3.Success, s3.Message);

        var closed = await _service.CloseTaskAsync(task.Id, 1, "Campaign delivered on time");
        Assert.True(closed.Success, closed.Message);
        Assert.Equal(99, closed.NewStatus);
    }

    private async Task<Domain.BaseTask> CreateMarketingTaskAsync()
    {
        var task = new Domain.BaseTask
        {
            TaskType = "Marketing",
            Description = "Plan and launch the spring campaign",
            CurrentStatus = 1,
            AssignedToUserId = 1,
            CustomDataJson = "{}"
        };
        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }
}

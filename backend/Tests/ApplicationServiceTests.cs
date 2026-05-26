using DanTaskManager.Data;
using DanTaskManager.Domain;
using DanTaskManager.Domain.Handlers;
using DanTaskManager.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DanTaskManager.Tests;

public class PageRequestTests
{
    [Theory]
    [InlineData(0, 0, 1, 20, 0)]
    [InlineData(-3, -10, 1, 20, 0)]
    [InlineData(2, 500, 2, 100, 100)]
    [InlineData(3, 10, 3, 10, 20)]
    public void PageRequest_NormalizesBoundsAndSkip(
        int page,
        int pageSize,
        int expectedPage,
        int expectedPageSize,
        int expectedSkip)
    {
        var request = new PageRequest(page, pageSize);

        Assert.Equal(expectedPage, request.NormalizedPage);
        Assert.Equal(expectedPageSize, request.NormalizedPageSize);
        Assert.Equal(expectedSkip, request.Skip);
    }
}

public class UserApplicationServiceTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsAlphabeticalPageWithAccurateTaskCounts()
    {
        await using var context = await TestDb.CreateContextAsync();
        var beta = new AppUser { Name = "Beta User", Email = "beta@example.com" };
        var alpha = new AppUser { Name = "Alpha User", Email = "alpha@example.com" };
        context.Users.AddRange(beta, alpha);
        await context.SaveChangesAsync();

        context.Tasks.AddRange(
            TestDb.TaskFor(beta.Id, "Open beta", currentStatus: 1),
            TestDb.TaskFor(beta.Id, "Closed beta", currentStatus: 99),
            TestDb.TaskFor(alpha.Id, "Open alpha", currentStatus: 2));
        await context.SaveChangesAsync();

        var service = new UserApplicationService(context);

        var page = await service.GetAllAsync(new PageRequest(Page: 1, PageSize: 1));
        var betaDetails = await service.GetByIdAsync(beta.Id);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        var firstUser = Assert.Single(page.Items);
        Assert.Equal(alpha.Id, firstUser.Id);
        Assert.Equal("Alpha User", firstUser.Name);
        Assert.Equal(1, firstUser.TasksCount);
        Assert.Equal(1, firstUser.OpenTasksCount);

        Assert.NotNull(betaDetails);
        Assert.Equal(2, betaDetails.TasksCount);
        Assert.Equal(1, betaDetails.OpenTasksCount);
    }

    [Fact]
    public async Task CreateAsync_WhenEmailAlreadyExists_ReturnsFailureWithoutAddingDuplicate()
    {
        await using var context = await TestDb.CreateContextAsync();
        context.Users.Add(new AppUser { Name = "Existing User", Email = "dupe@example.com" });
        await context.SaveChangesAsync();

        var service = new UserApplicationService(context);

        var result = await service.CreateAsync(new UserCreateCommand("Another User", "dupe@example.com"));

        Assert.False(result.Success);
        Assert.Contains("אימייל", result.Message);
        Assert.Equal(1, await context.Users.CountAsync(u => u.Email == "dupe@example.com"));
    }
}

public class TaskApplicationServiceTests
{
    [Fact]
    public async Task CreateAsync_WithBlankCustomData_CreatesTaskWithInitialStatusAndNormalizedJson()
    {
        await using var context = await TestDb.CreateContextAsync();
        var user = new AppUser { Name = "Owner", Email = "owner@example.com" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = TestDb.CreateTaskService(context);

        var result = await service.CreateAsync(
            new TaskCreateCommand("Procurement", "Buy keyboards", user.Id, "   "));

        Assert.True(result.Success);
        Assert.NotNull(result.CreatedTask);
        Assert.Equal("Procurement", result.CreatedTask.TaskType);
        Assert.Equal(0, result.CreatedTask.CurrentStatus);
        Assert.Equal("{}", result.CreatedTask.CustomDataJson);
        Assert.Equal(1, await context.Tasks.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_WithInvalidJson_ReturnsFailureWithoutPersistingTask()
    {
        await using var context = await TestDb.CreateContextAsync();
        var user = new AppUser { Name = "Owner", Email = "owner@example.com" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = TestDb.CreateTaskService(context);

        var result = await service.CreateAsync(
            new TaskCreateCommand("Procurement", "Buy monitors", user.Id, "{ invalid json"));

        Assert.False(result.Success);
        Assert.Contains("JSON", result.Message);
        Assert.Empty(await context.Tasks.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_WithMissingAssignedUser_ReturnsFailureWithoutPersistingTask()
    {
        await using var context = await TestDb.CreateContextAsync();
        var service = TestDb.CreateTaskService(context);

        var result = await service.CreateAsync(
            new TaskCreateCommand("Development", "Build feature", AssignedToUserId: 404, "{}"));

        Assert.False(result.Success);
        Assert.Contains("משתמש", result.Message);
        Assert.Empty(await context.Tasks.ToListAsync());
    }

    [Fact]
    public async Task GetOpenByUserAsync_ExcludesClosedAndOtherUsersAndKeepsNewestFirst()
    {
        await using var context = await TestDb.CreateContextAsync();
        var owner = new AppUser { Name = "Owner", Email = "owner@example.com" };
        var other = new AppUser { Name = "Other", Email = "other@example.com" };
        context.Users.AddRange(owner, other);
        await context.SaveChangesAsync();

        var olderOpen = TestDb.TaskFor(
            owner.Id,
            "Older open",
            currentStatus: 1,
            createdAt: new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc));
        var closedNewest = TestDb.TaskFor(
            owner.Id,
            "Closed newest",
            currentStatus: 99,
            createdAt: new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc));
        var newerOpen = TestDb.TaskFor(
            owner.Id,
            "Newer open",
            currentStatus: 2,
            createdAt: new DateTime(2026, 5, 2, 10, 0, 0, DateTimeKind.Utc));
        var otherUserTask = TestDb.TaskFor(
            other.Id,
            "Other user",
            currentStatus: 1,
            createdAt: new DateTime(2026, 5, 4, 10, 0, 0, DateTimeKind.Utc));
        context.Tasks.AddRange(olderOpen, closedNewest, newerOpen, otherUserTask);
        await context.SaveChangesAsync();

        var service = TestDb.CreateTaskService(context);

        var result = await service.GetOpenByUserAsync(owner.Id, new PageRequest(Page: 1, PageSize: 10));

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.Collection(
            result.Items,
            task => Assert.Equal(newerOpen.Id, task.Id),
            task => Assert.Equal(olderOpen.Id, task.Id));
        Assert.All(result.Items, task =>
        {
            Assert.Equal(owner.Id, task.AssignedToUserId);
            Assert.NotEqual(99, task.CurrentStatus);
        });
    }
}

internal static class TestDb
{
    public static async Task<ApplicationDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        context.Tasks.RemoveRange(context.Tasks);
        context.Users.RemoveRange(context.Users);
        await context.SaveChangesAsync();
        return context;
    }

    public static BaseTask TaskFor(
        int assignedToUserId,
        string description,
        int currentStatus,
        DateTime? createdAt = null)
    {
        var timestamp = createdAt ?? DateTime.UtcNow;

        return new BaseTask
        {
            TaskType = "Development",
            Description = description,
            CurrentStatus = currentStatus,
            AssignedToUserId = assignedToUserId,
            CustomDataJson = "{}",
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };
    }

    public static TaskApplicationService CreateTaskService(ApplicationDbContext context)
    {
        var handlers = new ITaskHandler[]
        {
            new ProcurementTaskHandler(),
            new DevelopmentTaskHandler()
        };

        return new TaskApplicationService(
            context,
            new StubWorkflowService(),
            new TaskHandlerFactory(handlers),
            NullLogger<TaskApplicationService>.Instance);
    }

    private sealed class StubWorkflowService : ITaskWorkflowService
    {
        public Task<WorkflowResult> ChangeStatusAsync(
            int taskId,
            int newStatus,
            string newDataJson,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(WorkflowResult.FailureResult("Not used by these tests"));
        }

        public Task<WorkflowResult> CloseTaskAsync(
            int taskId,
            string finalNotes,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(WorkflowResult.FailureResult("Not used by these tests"));
        }

        public Task<IEnumerable<BaseTask>> GetUserTasksAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Enumerable.Empty<BaseTask>());
        }

        public Task<BaseTask?> GetTaskAsync(int taskId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BaseTask?>(null);
        }
    }
}

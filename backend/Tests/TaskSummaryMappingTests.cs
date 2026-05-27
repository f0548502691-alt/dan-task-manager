using DanTaskManager.Data;
using DanTaskManager.Domain;
using DanTaskManager.Services;
using Microsoft.EntityFrameworkCore;

namespace DanTaskManager.Tests;

public class TaskSummaryMappingTests
{
    [Fact]
    public async Task TaskApplicationService_GetAllAsync_IncludesAssignedUserBrief()
    {
        var options = CreateOptions();
        await using var context = new ApplicationDbContext(options);
        var user = new AppUser
        {
            Id = 201,
            Name = "Mapping User",
            Email = "mapping@example.com"
        };
        context.Users.Add(user);
        context.Tasks.Add(new BaseTask
        {
            Id = 301,
            TaskType = "Development",
            Description = "Verify shared task mapping",
            CurrentStatus = WorkflowConstants.CreatedStatus,
            AssignedToUserId = user.Id,
            AssignedToUser = user,
            CustomDataJson = "{}",
            CreatedAt = new DateTime(2026, 5, 27, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 5, 27, 8, 30, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        var service = new TaskApplicationService(
            context,
            new NoOpWorkflowService(),
            new MockTaskApplicationLogger());

        var result = await service.GetAllAsync(new PageRequest(1, 10));

        var task = Assert.Single(result.Items);
        Assert.Equal(301, task.Id);
        Assert.Equal(201, task.AssignedToUserId);
        Assert.NotNull(task.AssignedToUser);
        Assert.Equal(user.Id, task.AssignedToUser.Id);
        Assert.Equal(user.Name, task.AssignedToUser.Name);
        Assert.Equal(user.Email, task.AssignedToUser.Email);
    }

    [Fact]
    public async Task UserApplicationService_GetUserTasksAsync_UsesSameSummaryMappingAndOrdering()
    {
        var options = CreateOptions();
        await using var context = new ApplicationDbContext(options);
        var user = new AppUser
        {
            Id = 202,
            Name = "Assigned User",
            Email = "assigned@example.com"
        };
        var otherUser = new AppUser
        {
            Id = 203,
            Name = "Other User",
            Email = "other@example.com"
        };

        context.Users.AddRange(user, otherUser);
        context.Tasks.AddRange(
            new BaseTask
            {
                Id = 302,
                TaskType = "Procurement",
                Description = "Older assigned task",
                CurrentStatus = WorkflowConstants.CreatedStatus,
                AssignedToUserId = user.Id,
                AssignedToUser = user,
                CustomDataJson = "{}",
                CreatedAt = new DateTime(2026, 5, 27, 7, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 5, 27, 7, 15, 0, DateTimeKind.Utc)
            },
            new BaseTask
            {
                Id = 303,
                TaskType = "Development",
                Description = "Newest assigned task",
                CurrentStatus = WorkflowConstants.CreatedStatus,
                AssignedToUserId = user.Id,
                AssignedToUser = user,
                CustomDataJson = "{}",
                CreatedAt = new DateTime(2026, 5, 27, 9, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 5, 27, 9, 15, 0, DateTimeKind.Utc)
            },
            new BaseTask
            {
                Id = 304,
                TaskType = "Development",
                Description = "Unrelated user task",
                CurrentStatus = WorkflowConstants.CreatedStatus,
                AssignedToUserId = otherUser.Id,
                AssignedToUser = otherUser,
                CustomDataJson = "{}",
                CreatedAt = new DateTime(2026, 5, 27, 10, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 5, 27, 10, 15, 0, DateTimeKind.Utc)
            });
        await context.SaveChangesAsync();

        var service = new UserApplicationService(context);

        var result = await service.GetUserTasksAsync(user.Id, new PageRequest(1, 10));

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(new[] { 303, 302 }, result.Items.Select(task => task.Id));
        Assert.All(result.Items, task =>
        {
            Assert.Equal(user.Id, task.AssignedToUserId);
            Assert.NotNull(task.AssignedToUser);
            Assert.Equal(user.Name, task.AssignedToUser.Name);
            Assert.Equal(user.Email, task.AssignedToUser.Email);
        });
    }

    private static DbContextOptions<ApplicationDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"TaskSummaryMappingTests-{Guid.NewGuid()}")
            .Options;
    }

    private class NoOpWorkflowService : ITaskWorkflowService
    {
        public Task<WorkflowResult> ChangeStatusAsync(
            int taskId,
            int newStatus,
            int nextAssignedToUserId,
            string newDataJson,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<WorkflowResult> CloseTaskAsync(
            int taskId,
            int nextAssignedToUserId,
            string finalNotes,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<WorkflowResult> EnsureTaskMutableAsync(
            int taskId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IEnumerable<BaseTask>> GetUserTasksAsync(
            int userId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<BaseTask?> GetTaskAsync(int taskId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class MockTaskApplicationLogger : ILogger<TaskApplicationService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) { }
    }
}

using System.Reflection;
using DanTaskManager.Controllers;
using DanTaskManager.Data;
using DanTaskManager.Domain;
using DanTaskManager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DanTaskManager.Tests;

public class UsersControllerContractTests
{
    [Fact]
    public void UsersController_DoesNotExposeUserCreationEndpoint()
    {
        var postActions = typeof(UsersController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpPostAttribute>().Any())
            .Select(method => method.Name);

        Assert.Empty(postActions);
        Assert.Null(typeof(UsersController).Assembly.GetType("DanTaskManager.Controllers.CreateUserRequest"));
    }

    [Fact]
    public void UserApplicationServiceContract_DoesNotExposeCreationTypes()
    {
        Assert.DoesNotContain(
            typeof(IUserApplicationService).GetMethods(),
            method => method.Name == "CreateAsync");

        var assembly = typeof(IUserApplicationService).Assembly;
        Assert.Null(assembly.GetType("DanTaskManager.Services.UserCreateCommand"));
        Assert.Null(assembly.GetType("DanTaskManager.Services.UserCreationResult"));
    }
}

public class UserApplicationServiceTests : IAsyncLifetime
{
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private ApplicationDbContext _context = null!;
    private UserApplicationService _service = null!;

    public UserApplicationServiceTests()
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"UserApplicationServiceTests-{Guid.NewGuid()}")
            .Options;
    }

    public async Task InitializeAsync()
    {
        _context = new ApplicationDbContext(_options);

        var zed = new AppUser
        {
            Id = 1,
            Name = "Zed User",
            Email = "zed@example.com",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var alice = new AppUser
        {
            Id = 2,
            Name = "Alice User",
            Email = "alice@example.com",
            CreatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        };

        var openZedTask = new BaseTask
        {
            Id = 10,
            TaskType = "Development",
            CurrentStatus = 1,
            AssignedToUserId = zed.Id,
            AssignedToUser = zed,
            Description = "Open Zed task",
            CustomDataJson = "{}",
            CreatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var closedZedTask = new BaseTask
        {
            Id = 11,
            TaskType = "Procurement",
            CurrentStatus = WorkflowConstants.ClosedStatus,
            AssignedToUserId = zed.Id,
            AssignedToUser = zed,
            Description = "Closed Zed task",
            CustomDataJson = "{}",
            CreatedAt = new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc)
        };
        var aliceTask = new BaseTask
        {
            Id = 12,
            TaskType = "Analysis",
            CurrentStatus = 2,
            AssignedToUserId = alice.Id,
            AssignedToUser = alice,
            Description = "Alice task",
            CustomDataJson = "{}",
            CreatedAt = new DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc)
        };

        _context.Users.AddRange(zed, alice);
        _context.Tasks.AddRange(openZedTask, closedZedTask, aliceTask);
        await _context.SaveChangesAsync();

        _service = new UserApplicationService(_context);
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetAllAsync_ReturnsExistingUsersWithTaskCounts()
    {
        var result = await _service.GetAllAsync(new PageRequest(Page: 1, PageSize: 10));

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Collection(
            result.Items,
            alice =>
            {
                Assert.Equal(2, alice.Id);
                Assert.Equal("Alice User", alice.Name);
                Assert.Equal(1, alice.TasksCount);
                Assert.Equal(1, alice.OpenTasksCount);
            },
            zed =>
            {
                Assert.Equal(1, zed.Id);
                Assert.Equal("Zed User", zed.Name);
                Assert.Equal(2, zed.TasksCount);
                Assert.Equal(1, zed.OpenTasksCount);
            });
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserDoesNotExist_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserTasksAsync_ReturnsOnlyRequestedUsersTasksOrderedNewestFirst()
    {
        var result = await _service.GetUserTasksAsync(1, new PageRequest(Page: 1, PageSize: 10));

        Assert.Equal(2, result.TotalCount);
        Assert.Collection(
            result.Items,
            first =>
            {
                Assert.Equal(11, first.Id);
                Assert.Equal("Closed Zed task", first.Description);
                Assert.Equal(1, first.AssignedToUserId);
            },
            second =>
            {
                Assert.Equal(10, second.Id);
                Assert.Equal("Open Zed task", second.Description);
                Assert.Equal(1, second.AssignedToUserId);
            });
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalseForUnknownUser()
    {
        var exists = await _service.ExistsAsync(999);

        Assert.False(exists);
    }
}

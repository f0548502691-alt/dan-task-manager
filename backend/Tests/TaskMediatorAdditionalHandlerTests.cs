using DanTaskManager.Application.Tasks.CloseTask;
using DanTaskManager.Application.Tasks.DeleteTask;
using DanTaskManager.Application.Tasks.GetAllTasks;
using DanTaskManager.Application.Tasks.GetTasksByType;
using DanTaskManager.Application.Tasks.GetTasksByUser;
using DanTaskManager.Application.Tasks.UpdateTaskDescription;
using DanTaskManager.Application.Tasks.UserExists;
using DanTaskManager.Services;
using Moq;

namespace DanTaskManager.Tests;

public class GetAllTasksQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_DelegatesToTaskApplicationService()
    {
        var serviceMock = new Mock<ITaskApplicationService>();
        var pageRequest = new PageRequest(2, 15);
        var expectedResult = new PagedResult<TaskSummaryDto>();

        serviceMock
            .Setup(service => service.GetAllAsync(pageRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var handler = new GetAllTasksQueryHandler(serviceMock.Object);

        var result = await handler.Handle(new GetAllTasksQuery(pageRequest), CancellationToken.None);

        Assert.Same(expectedResult, result);
        serviceMock.Verify(service => service.GetAllAsync(pageRequest, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class GetTasksByTypeQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_DelegatesToTaskApplicationService()
    {
        var serviceMock = new Mock<ITaskApplicationService>();
        var pageRequest = new PageRequest(1, 20);
        var taskType = "Development";
        var expectedResult = new PagedResult<TaskSummaryDto>();

        serviceMock
            .Setup(service => service.GetByTypeAsync(taskType, pageRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var handler = new GetTasksByTypeQueryHandler(serviceMock.Object);

        var result = await handler.Handle(new GetTasksByTypeQuery(taskType, pageRequest), CancellationToken.None);

        Assert.Same(expectedResult, result);
        serviceMock.Verify(
            service => service.GetByTypeAsync(taskType, pageRequest, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public class GetTasksByUserQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_DelegatesToTaskApplicationService()
    {
        var serviceMock = new Mock<ITaskApplicationService>();
        var pageRequest = new PageRequest(3, 10);
        var userId = 9;
        var expectedResult = new PagedResult<TaskSummaryDto>();

        serviceMock
            .Setup(service => service.GetByUserAsync(userId, pageRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var handler = new GetTasksByUserQueryHandler(serviceMock.Object);

        var result = await handler.Handle(new GetTasksByUserQuery(userId, pageRequest), CancellationToken.None);

        Assert.Same(expectedResult, result);
        serviceMock.Verify(
            service => service.GetByUserAsync(userId, pageRequest, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public class UserExistsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_DelegatesToTaskApplicationService()
    {
        var serviceMock = new Mock<ITaskApplicationService>();
        const int userId = 14;

        serviceMock
            .Setup(service => service.UserExistsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new UserExistsQueryHandler(serviceMock.Object);

        var result = await handler.Handle(new UserExistsQuery(userId), CancellationToken.None);

        Assert.True(result);
        serviceMock.Verify(service => service.UserExistsAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class CloseTaskCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_DelegatesToTaskApplicationService()
    {
        var serviceMock = new Mock<ITaskApplicationService>();
        const int taskId = 11;
        const int nextAssignedToUserId = 4;
        const string notes = "Completed successfully";
        var expectedResult = WorkflowResult.SuccessResult(
            newStatus: 99,
            task: new global::DanTaskManager.Domain.BaseTask(),
            message: "Closed");

        serviceMock
            .Setup(service => service.CloseAsync(taskId, nextAssignedToUserId, notes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var handler = new CloseTaskCommandHandler(serviceMock.Object);

        var result = await handler.Handle(
            new CloseTaskCommand(taskId, nextAssignedToUserId, notes),
            CancellationToken.None);

        Assert.Same(expectedResult, result);
        serviceMock.Verify(
            service => service.CloseAsync(taskId, nextAssignedToUserId, notes, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public class UpdateTaskDescriptionCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_DelegatesToTaskApplicationService()
    {
        var serviceMock = new Mock<ITaskApplicationService>();
        const int taskId = 21;
        const string description = "Updated description";

        serviceMock
            .Setup(service => service.UpdateDescriptionAsync(taskId, description, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new UpdateTaskDescriptionCommandHandler(serviceMock.Object);

        var result = await handler.Handle(
            new UpdateTaskDescriptionCommand(taskId, description),
            CancellationToken.None);

        Assert.True(result);
        serviceMock.Verify(
            service => service.UpdateDescriptionAsync(taskId, description, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public class DeleteTaskCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_DelegatesToTaskApplicationService()
    {
        var serviceMock = new Mock<ITaskApplicationService>();
        const int taskId = 31;

        serviceMock
            .Setup(service => service.DeleteAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new DeleteTaskCommandHandler(serviceMock.Object);

        var result = await handler.Handle(new DeleteTaskCommand(taskId), CancellationToken.None);

        Assert.True(result);
        serviceMock.Verify(service => service.DeleteAsync(taskId, It.IsAny<CancellationToken>()), Times.Once);
    }
}

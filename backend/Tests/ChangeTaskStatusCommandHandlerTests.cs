using DanTaskManager.Application.Tasks.ChangeTaskStatus;
using DanTaskManager.Services;
using Moq;
using Xunit;

namespace DanTaskManager.Tests;

public class ChangeTaskStatusCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_DelegatesToTaskApplicationService()
    {
        // Arrange
        var serviceMock = new Mock<ITaskApplicationService>();
        var expectedResult = WorkflowResult.SuccessResult(newStatus: 2, task: new global::DanTaskManager.Domain.BaseTask(), message: "ok");

        serviceMock
            .Setup(service => service.ChangeStatusAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var handler = new ChangeTaskStatusCommandHandler(serviceMock.Object);
        var command = new ChangeTaskStatusCommand(
            TaskId: 33,
            NewStatus: 2,
            NextAssignedToUserId: 5,
            CustomDataJson: "{\"receipt\":\"R-1\"}");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Same(expectedResult, result);
        serviceMock.Verify(service => service.ChangeStatusAsync(
                command.TaskId,
                command.NewStatus,
                command.NextAssignedToUserId,
                command.CustomDataJson,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

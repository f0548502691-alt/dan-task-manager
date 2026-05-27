using DanTaskManager.Application.Tasks.GetTaskById;
using DanTaskManager.Services;
using Moq;

namespace DanTaskManager.Tests;

public class GetTaskByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenTaskExists_ReturnsTaskFromApplicationService()
    {
        // Arrange
        var serviceMock = new Mock<ITaskApplicationService>();
        var expectedTask = new TaskDetailsDto { Id = 123, TaskType = "Analysis" };

        serviceMock
            .Setup(service => service.GetByIdAsync(expectedTask.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTask);

        var handler = new GetTaskByIdQueryHandler(serviceMock.Object);

        // Act
        var result = await handler.Handle(new GetTaskByIdQuery(expectedTask.Id), CancellationToken.None);

        // Assert
        Assert.Same(expectedTask, result);
        serviceMock.Verify(service => service.GetByIdAsync(expectedTask.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}

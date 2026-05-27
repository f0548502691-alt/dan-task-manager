using DanTaskManager.Application.Tasks.CreateTask;
using DanTaskManager.Services;
using Moq;

namespace DanTaskManager.Tests;

public class CreateTaskCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_DelegatesToTaskApplicationService()
    {
        // Arrange
        var serviceMock = new Mock<ITaskApplicationService>();
        var expectedResult = TaskCreationResult.SuccessResult(new TaskDetailsDto { Id = 42 });

        serviceMock
            .Setup(service => service.CreateAsync(
                It.IsAny<TaskCreateCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var handler = new CreateTaskCommandHandler(serviceMock.Object);
        var command = new CreateTaskCommand(
            "Development",
            "Implement MediatR migration",
            7,
            "{\"priority\":\"high\"}");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Same(expectedResult, result);
        serviceMock.Verify(service => service.CreateAsync(
                It.Is<TaskCreateCommand>(taskCommand =>
                    taskCommand.TaskType == command.TaskType &&
                    taskCommand.Description == command.Description &&
                    taskCommand.AssignedToUserId == command.AssignedToUserId &&
                    taskCommand.CustomDataJson == command.CustomDataJson),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

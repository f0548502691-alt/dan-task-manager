using MediatR;

namespace DanTaskManager.Application.Tasks.DeleteTask;

public record DeleteTaskCommand(int TaskId) : IRequest<bool>;

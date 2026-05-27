using MediatR;

namespace DanTaskManager.Application.Tasks.UpdateTaskDescription;

public record UpdateTaskDescriptionCommand(int TaskId, string? Description) : IRequest<bool>;

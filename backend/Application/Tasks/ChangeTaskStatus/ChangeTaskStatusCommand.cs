using DanTaskManager.Services;
using MediatR;

namespace DanTaskManager.Application.Tasks.ChangeTaskStatus;

public record ChangeTaskStatusCommand(
    int TaskId,
    int NewStatus,
    int NextAssignedToUserId,
    string CustomDataJson) : IRequest<WorkflowResult>;

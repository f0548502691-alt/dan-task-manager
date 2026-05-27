using DanTaskManager.Services;
using MediatR;

namespace DanTaskManager.Application.Tasks.CloseTask;

public record CloseTaskCommand(
    int TaskId,
    int NextAssignedToUserId,
    string FinalNotes) : IRequest<WorkflowResult>;

using DanTaskManager.Services;
using MediatR;

namespace DanTaskManager.Application.Tasks.CloseTask;

public record CloseTaskCommand(int TaskId, string FinalNotes) : IRequest<WorkflowResult>;

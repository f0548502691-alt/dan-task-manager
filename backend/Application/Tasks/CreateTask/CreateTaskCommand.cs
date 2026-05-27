using DanTaskManager.Services;
using MediatR;

namespace DanTaskManager.Application.Tasks.CreateTask;

public record CreateTaskCommand(
    string TaskType,
    string Description,
    int AssignedToUserId,
    string CustomDataJson) : IRequest<TaskCreationResult>;

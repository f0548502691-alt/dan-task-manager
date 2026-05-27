using DanTaskManager.Services;
using MediatR;

namespace DanTaskManager.Application.Tasks.GetTaskById;

public record GetTaskByIdQuery(int TaskId) : IRequest<TaskDetailsDto?>;

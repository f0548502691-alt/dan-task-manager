using DanTaskManager.Services;
using MediatR;

namespace DanTaskManager.Application.Tasks.GetTasksByType;

public record GetTasksByTypeQuery(string TaskType, PageRequest PageRequest) : IRequest<PagedResult<TaskSummaryDto>>;

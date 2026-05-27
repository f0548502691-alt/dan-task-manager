using DanTaskManager.Services;
using MediatR;

namespace DanTaskManager.Application.Tasks.GetAllTasks;

public record GetAllTasksQuery(PageRequest PageRequest) : IRequest<PagedResult<TaskSummaryDto>>;

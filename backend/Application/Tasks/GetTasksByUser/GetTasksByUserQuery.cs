using DanTaskManager.Services;
using MediatR;

namespace DanTaskManager.Application.Tasks.GetTasksByUser;

public record GetTasksByUserQuery(int UserId, PageRequest PageRequest) : IRequest<PagedResult<TaskSummaryDto>>;

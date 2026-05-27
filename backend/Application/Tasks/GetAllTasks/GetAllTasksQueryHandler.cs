using DanTaskManager.Services;
using MediatR;

namespace DanTaskManager.Application.Tasks.GetAllTasks;

public class GetAllTasksQueryHandler : IRequestHandler<GetAllTasksQuery, PagedResult<TaskSummaryDto>>
{
    private readonly ITaskApplicationService _taskApplicationService;

    public GetAllTasksQueryHandler(ITaskApplicationService taskApplicationService)
    {
        _taskApplicationService = taskApplicationService;
    }

    public Task<PagedResult<TaskSummaryDto>> Handle(GetAllTasksQuery request, CancellationToken cancellationToken)
    {
        return _taskApplicationService.GetAllAsync(request.PageRequest, cancellationToken);
    }
}

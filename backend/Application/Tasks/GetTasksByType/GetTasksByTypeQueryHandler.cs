using DanTaskManager.Services;
using MediatR;

namespace DanTaskManager.Application.Tasks.GetTasksByType;

public class GetTasksByTypeQueryHandler : IRequestHandler<GetTasksByTypeQuery, PagedResult<TaskSummaryDto>>
{
    private readonly ITaskApplicationService _taskApplicationService;

    public GetTasksByTypeQueryHandler(ITaskApplicationService taskApplicationService)
    {
        _taskApplicationService = taskApplicationService;
    }

    public Task<PagedResult<TaskSummaryDto>> Handle(GetTasksByTypeQuery request, CancellationToken cancellationToken)
    {
        return _taskApplicationService.GetByTypeAsync(request.TaskType, request.PageRequest, cancellationToken);
    }
}

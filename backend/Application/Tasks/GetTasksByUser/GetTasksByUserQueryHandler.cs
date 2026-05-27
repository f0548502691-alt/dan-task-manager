using DanTaskManager.Services;
using MediatR;

namespace DanTaskManager.Application.Tasks.GetTasksByUser;

public class GetTasksByUserQueryHandler : IRequestHandler<GetTasksByUserQuery, PagedResult<TaskSummaryDto>>
{
    private readonly ITaskApplicationService _taskApplicationService;

    public GetTasksByUserQueryHandler(ITaskApplicationService taskApplicationService)
    {
        _taskApplicationService = taskApplicationService;
    }

    public Task<PagedResult<TaskSummaryDto>> Handle(GetTasksByUserQuery request, CancellationToken cancellationToken)
    {
        return _taskApplicationService.GetByUserAsync(request.UserId, request.PageRequest, cancellationToken);
    }
}

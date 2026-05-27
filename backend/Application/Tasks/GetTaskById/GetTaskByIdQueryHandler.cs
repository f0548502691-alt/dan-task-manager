using DanTaskManager.Services;
using MediatR;

namespace DanTaskManager.Application.Tasks.GetTaskById;

public class GetTaskByIdQueryHandler : IRequestHandler<GetTaskByIdQuery, TaskDetailsDto?>
{
    private readonly ITaskApplicationService _taskApplicationService;

    public GetTaskByIdQueryHandler(ITaskApplicationService taskApplicationService)
    {
        _taskApplicationService = taskApplicationService;
    }

    public Task<TaskDetailsDto?> Handle(GetTaskByIdQuery request, CancellationToken cancellationToken)
    {
        return _taskApplicationService.GetByIdAsync(request.TaskId, cancellationToken);
    }
}

using DanTaskManager.Domain;
using System.Linq.Expressions;

namespace DanTaskManager.Services;

internal static class TaskDtoMappings
{
    public static Expression<Func<BaseTask, TaskSummaryDto>> ToTaskSummary()
    {
        return task => new TaskSummaryDto
        {
            Id = task.Id,
            TaskType = task.TaskType,
            CurrentStatus = task.CurrentStatus,
            AssignedToUserId = task.AssignedToUserId,
            Description = task.Description,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            AssignedToUser = task.AssignedToUser == null
                ? null
                : new UserBriefDto
                {
                    Id = task.AssignedToUser.Id,
                    Name = task.AssignedToUser.Name,
                    Email = task.AssignedToUser.Email
                }
        };
    }
}

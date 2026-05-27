using DanTaskManager.Domain;

namespace DanTaskManager.Services;

internal static class TaskDtoMappings
{
    public static TaskSummaryDto ToTaskSummaryDto(BaseTask task)
    {
        return new TaskSummaryDto
        {
            Id = task.Id,
            TaskType = task.TaskType,
            CurrentStatus = task.CurrentStatus,
            AssignedToUserId = task.AssignedToUserId,
            Description = task.Description,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            AssignedToUser = ToUserBriefDto(task.AssignedToUser)
        };
    }

    public static TaskDetailsDto ToTaskDetailsDto(BaseTask task)
    {
        return new TaskDetailsDto
        {
            Id = task.Id,
            TaskType = task.TaskType,
            CurrentStatus = task.CurrentStatus,
            AssignedToUserId = task.AssignedToUserId,
            Description = task.Description,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            CustomFields = ParseCustomFields(task.CustomDataJson),
            AssignedToUser = ToUserBriefDto(task.AssignedToUser)
        };
    }

    private static UserBriefDto? ToUserBriefDto(AppUser? user)
    {
        return user == null
            ? null
            : new UserBriefDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email
            };
    }

    private static System.Text.Json.JsonElement ParseCustomFields(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.Clone();
        }
        catch (System.Text.Json.JsonException)
        {
            return System.Text.Json.JsonSerializer.SerializeToElement(new Dictionary<string, object?>());
        }
    }
}

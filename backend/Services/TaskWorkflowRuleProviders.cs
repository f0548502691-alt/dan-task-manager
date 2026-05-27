using DanTaskManager.Domain;
using DanTaskManager.Domain.Handlers;

namespace DanTaskManager.Services;

/// <summary>
/// מקור כללי workflow עבור סוג משימה נתון.
/// מאפשר להוסיף ספקי חוקים נוספים בלי לשנות את שירות ה-workflow.
/// </summary>
public interface ITaskWorkflowRuleProvider
{
    int Priority { get; }
    bool CanHandle(string taskType);
    int? GetFinalStatus(string taskType);
    ValidationResult ValidateStatusChange(BaseTask task, int nextStatus, string newDataJson);
    string BuildCloseData(BaseTask task, string finalNotes);
}

internal static class WorkflowCloseData
{
    public static string Merge(BaseTask task, string finalNotes)
    {
        var updatedJson = task.CustomDataJson;
        try
        {
            var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(updatedJson) ?? new();
            data["finalNotes"] = finalNotes;
            data["closedAt"] = DateTime.UtcNow.ToString("o");
            return System.Text.Json.JsonSerializer.Serialize(data);
        }
        catch (System.Text.Json.JsonException)
        {
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                finalNotes,
                closedAt = DateTime.UtcNow.ToString("o")
            });
        }
    }
}

public class MetadataTaskWorkflowRuleProvider : ITaskWorkflowRuleProvider
{
    private readonly ITaskTypeValidationService _validationService;

    public MetadataTaskWorkflowRuleProvider(ITaskTypeValidationService validationService)
    {
        _validationService = validationService;
    }

    public int Priority => 0;

    public bool CanHandle(string taskType) => _validationService.HasTaskType(taskType);

    public int? GetFinalStatus(string taskType) => _validationService.GetFinalStatus(taskType);

    public ValidationResult ValidateStatusChange(BaseTask task, int nextStatus, string newDataJson)
    {
        return _validationService.ValidateStatusData(task.TaskType, nextStatus, newDataJson);
    }

    public string BuildCloseData(BaseTask task, string finalNotes) => WorkflowCloseData.Merge(task, finalNotes);
}

public class HandlerTaskWorkflowRuleProvider : ITaskWorkflowRuleProvider
{
    private readonly TaskHandlerFactory _handlerFactory;

    public HandlerTaskWorkflowRuleProvider(TaskHandlerFactory handlerFactory)
    {
        _handlerFactory = handlerFactory;
    }

    public int Priority => 100;

    public bool CanHandle(string taskType) => _handlerFactory.HasHandler(taskType);

    public int? GetFinalStatus(string taskType)
    {
        return _handlerFactory.GetHandler(taskType)?.FinalStatus;
    }

    public ValidationResult ValidateStatusChange(BaseTask task, int nextStatus, string newDataJson)
    {
        var handler = _handlerFactory.GetHandler(task.TaskType);
        if (handler == null)
        {
            return ValidationResult.Failure($"Unsupported task type: {task.TaskType}");
        }

        return handler.ValidateStatusChange(
            task.CustomDataJson,
            task.CurrentStatus,
            nextStatus,
            newDataJson);
    }

    public string BuildCloseData(BaseTask task, string finalNotes) => WorkflowCloseData.Merge(task, finalNotes);
}

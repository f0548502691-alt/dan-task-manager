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
            return ValidationResult.Failure($"סוג משימה לא נתמך: {task.TaskType}");
        }

        return handler.ValidateStatusChange(
            task.CustomDataJson,
            task.CurrentStatus,
            nextStatus,
            newDataJson);
    }
}

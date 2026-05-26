namespace DanTaskManager.Domain.Handlers;

public abstract class StatusValidationTaskHandlerBase : ITaskHandler
{
    private readonly IReadOnlyDictionary<int, Func<string, ValidationResult>> _statusValidators;

    protected StatusValidationTaskHandlerBase(
        IReadOnlyDictionary<int, Func<string, ValidationResult>> statusValidators)
    {
        _statusValidators = statusValidators;
    }

    public abstract string TaskType { get; }
    public abstract int FinalStatus { get; }

    public ValidationResult ValidateStatusChange(
        string currentDataJson,
        int currentStatus,
        int nextStatus,
        string newDataJson)
    {
        if (currentStatus >= FinalStatus && nextStatus > currentStatus)
        {
            return ValidationResult.Failure(
                $"משימת {TaskType} נמצאת בסטטוס סופי {FinalStatus}; לא ניתן להעביר אותה לסטטוס {nextStatus}");
        }

        if (_statusValidators.TryGetValue(nextStatus, out var validator))
        {
            return validator(newDataJson);
        }

        return ValidationResult.Success();
    }
}

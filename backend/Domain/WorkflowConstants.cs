namespace DanTaskManager.Domain;

public static class WorkflowConstants
{
    public const int CreatedStatus = 1;
    public const int ClosedStatus = 99;

    public static readonly string[] SupportedTaskTypes =
    {
        "Procurement",
        "Development"
    };

    public static bool IsSupportedTaskType(string? taskType)
    {
        return !string.IsNullOrWhiteSpace(taskType) &&
               SupportedTaskTypes.Contains(taskType, StringComparer.OrdinalIgnoreCase);
    }
}

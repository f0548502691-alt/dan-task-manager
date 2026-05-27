namespace DanTaskManager.Services;

/// <summary>
/// Stable, machine-readable codes for workflow failures. Strings rather than
/// an enum so values can be reused as the public `code` field in error
/// responses without an additional mapping layer, and so callers (frontend,
/// tests) can compare against constants instead of localized messages.
/// </summary>
public static class WorkflowErrorCodes
{
    public const string TaskNotFound = "task_not_found";
    public const string TaskClosed = "task_closed";
    public const string TaskAlreadyClosed = "task_already_closed";
    public const string AssigneeNotFound = "assignee_not_found";
    public const string InvalidJsonPayload = "invalid_custom_data_json";
    public const string UnsupportedTaskType = "unsupported_task_type";
    public const string IllegalStatusTransition = "illegal_status_transition";
    public const string FinalStatusReached = "final_status_reached";
    public const string CloseRequiresFinalStatus = "close_requires_final_status";
    public const string CloseViaCloseTaskOnly = "close_via_close_task_only";
    public const string SameStatus = "same_status";
    public const string FieldValidationFailed = "field_validation_failed";
}

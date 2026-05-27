namespace DanTaskManager.Domain;

/// <summary>
/// חריגה עסקית עבור כשל וולידציה ב-Workflow.
/// מטופלת ברמת middleware וממופה ל-400 Bad Request.
/// </summary>
public class WorkflowValidationException : ApiValidationException
{
    public WorkflowValidationException(string message)
        : base(message, "workflow_validation_failed")
    {
    }
}

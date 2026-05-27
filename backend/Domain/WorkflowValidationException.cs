namespace DanTaskManager.Domain;

/// <summary>
/// Business-rule failure raised by the workflow service. Caught by the global
/// exception middleware and serialized as a 400 response with the stable
/// <c>code</c> field preserved from the originating <see cref="WorkflowResult"/>.
/// </summary>
public class WorkflowValidationException : ApiValidationException
{
    public WorkflowValidationException(string message, string code = "workflow_validation_failed")
        : base(message, string.IsNullOrWhiteSpace(code) ? "workflow_validation_failed" : code)
    {
    }
}

namespace DanTaskManager.Domain;

public class ApiValidationException : ApiException
{
    public ApiValidationException(string message, string code = "validation_failed")
        : base(400, code, message)
    {
    }
}

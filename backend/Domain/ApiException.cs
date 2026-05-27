namespace DanTaskManager.Domain;

/// <summary>
/// Base API exception with status-code and stable error code.
/// </summary>
public class ApiException : Exception
{
    public ApiException(int statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int StatusCode { get; }

    public string Code { get; }
}

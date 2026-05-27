namespace DanTaskManager.Domain;

public class ApiNotFoundException : ApiException
{
    public ApiNotFoundException(string message, string code = "not_found")
        : base(404, code, message)
    {
    }
}

using DanTaskManager.Domain;
using System.Text.Json;

namespace DanTaskManager.Middleware;

/// <summary>
/// Middleware גלובלי לטיפול בשגיאות API בפורמט עקבי עבור ה-UI.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "API exception for path: {Path}", context.Request.Path);
            await WriteErrorResponseAsync(
                context,
                ex.StatusCode,
                ex.Message,
                ex.Code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for path: {Path}", context.Request.Path);
            await WriteErrorResponseAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "An unexpected server error occurred",
                "internal_server_error");
        }
    }

    private static async Task WriteErrorResponseAsync(
        HttpContext context,
        int statusCode,
        string errorMessage,
        string code)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var payload = new
        {
            error = errorMessage,
            code
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}

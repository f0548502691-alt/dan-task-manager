using DanTaskManager.Domain;
using DanTaskManager.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace DanTaskManager.Tests;

public class GlobalExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenApiExceptionIsThrown_WritesStatusAndStableErrorCode()
    {
        var (context, payload) = await InvokeMiddlewareAsync(
            new ApiValidationException("Invalid task type", "task_type_validation_failed"));
        using (payload)
        {
            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            Assert.Equal("application/json", context.Response.ContentType);
            Assert.Equal("Invalid task type", payload.RootElement.GetProperty("error").GetString());
            Assert.Equal("task_type_validation_failed", payload.RootElement.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task InvokeAsync_WhenUnexpectedExceptionIsThrown_WritesGenericInternalServerErrorCode()
    {
        var (context, payload) = await InvokeMiddlewareAsync(new InvalidOperationException("Database unavailable"));
        using (payload)
        {
            Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
            Assert.Equal("application/json", context.Response.ContentType);
            Assert.False(string.IsNullOrWhiteSpace(payload.RootElement.GetProperty("error").GetString()));
            Assert.Equal("internal_server_error", payload.RootElement.GetProperty("code").GetString());
        }
    }

    private static async Task<(DefaultHttpContext Context, JsonDocument Payload)> InvokeMiddlewareAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/tasks";
        context.Response.Body = new MemoryStream();

        var middleware = new GlobalExceptionMiddleware(
            _ => Task.FromException(exception),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var payload = await JsonDocument.ParseAsync(context.Response.Body);
        return (context, payload);
    }
}

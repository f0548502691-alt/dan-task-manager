using DanTaskManager.Domain;
using DanTaskManager.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace DanTaskManager.Tests;

public class GlobalExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenWorkflowValidationExceptionThrown_ReturnsBadRequestJsonWithWorkflowCode()
    {
        // Arrange
        var context = CreateHttpContext();
        RequestDelegate next = _ => throw new WorkflowValidationException("תנועה לא חוקית");
        var middleware = new GlobalExceptionMiddleware(
            next,
            NullLogger<GlobalExceptionMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var payload = await ReadJsonResponseAsync(context);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
        Assert.Equal("תנועה לא חוקית", payload.RootElement.GetProperty("error").GetString());
        Assert.Equal("workflow_validation_failed", payload.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledExceptionThrown_ReturnsInternalServerErrorJsonWithGenericCode()
    {
        // Arrange
        var context = CreateHttpContext();
        RequestDelegate next = _ => throw new InvalidOperationException("database unavailable");
        var middleware = new GlobalExceptionMiddleware(
            next,
            NullLogger<GlobalExceptionMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var payload = await ReadJsonResponseAsync(context);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
        Assert.Equal("אירעה שגיאה בלתי צפויה בשרת", payload.RootElement.GetProperty("error").GetString());
        Assert.Equal("internal_server_error", payload.RootElement.GetProperty("code").GetString());
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonDocument> ReadJsonResponseAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(context.Response.Body);
    }
}

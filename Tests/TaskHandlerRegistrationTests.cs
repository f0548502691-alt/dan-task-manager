using DanTaskManager.Domain.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace DanTaskManager.Tests;

public class TaskHandlerRegistrationTests
{
    [Fact]
    public void AddTaskHandlersFromAssembly_RegistersAllConcreteTaskHandlers()
    {
        var services = new ServiceCollection();

        services.AddTaskHandlersFromAssembly();

        using var provider = services.BuildServiceProvider();
        var handlers = provider.GetServices<ITaskHandler>().ToArray();
        var taskTypes = handlers.Select(handler => handler.TaskType).ToArray();

        Assert.Contains(handlers, handler => handler is ProcurementTaskHandler);
        Assert.Contains(handlers, handler => handler is DevelopmentTaskHandler);
        Assert.Contains(handlers, handler => handler is AnalysisTaskHandler);
        Assert.Contains(handlers, handler => handler is TestingTaskHandler);
        Assert.Equal(taskTypes.Length, taskTypes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void AddTaskHandlersFromAssembly_AllowsFactoryToResolveDiscoveredHandlersCaseInsensitively()
    {
        var services = new ServiceCollection();
        services.AddTaskHandlersFromAssembly();

        using var provider = services.BuildServiceProvider();
        var factory = new TaskHandlerFactory(provider.GetServices<ITaskHandler>());

        Assert.IsType<AnalysisTaskHandler>(factory.GetHandler("analysis"));
        Assert.IsType<TestingTaskHandler>(factory.GetHandler("TESTING"));
    }
}

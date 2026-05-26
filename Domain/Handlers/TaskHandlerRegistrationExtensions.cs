using Microsoft.Extensions.DependencyInjection;

namespace DanTaskManager.Domain.Handlers;

public static class TaskHandlerRegistrationExtensions
{
    /// <summary>
    /// Registers all concrete ITaskHandler implementations from the handlers assembly.
    /// This keeps Program.cs closed for future task-type additions.
    /// </summary>
    public static IServiceCollection AddTaskHandlersFromAssembly(this IServiceCollection services)
    {
        var handlerTypes = typeof(ITaskHandler)
            .Assembly
            .GetTypes()
            .Where(t => typeof(ITaskHandler).IsAssignableFrom(t))
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => t.Namespace == typeof(ITaskHandler).Namespace)
            .Where(t => t.Name.EndsWith("TaskHandler", StringComparison.Ordinal));

        foreach (var handlerType in handlerTypes)
        {
            services.AddTransient(typeof(ITaskHandler), handlerType);
        }

        return services;
    }
}

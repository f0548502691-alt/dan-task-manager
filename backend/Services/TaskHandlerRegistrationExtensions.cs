using DanTaskManager.Domain.Handlers;
using System.Reflection;

namespace DanTaskManager.Services;

public static class TaskHandlerRegistrationExtensions
{
    public static IServiceCollection AddTaskHandlersFromAssembly(
        this IServiceCollection services,
        Assembly assembly)
    {
        var handlerInterface = typeof(ITaskHandler);

        var handlerImplementations = assembly
            .GetTypes()
            .Where(type =>
                handlerInterface.IsAssignableFrom(type) &&
                !type.IsInterface &&
                !type.IsAbstract)
            .OrderBy(type => type.Name)
            .ToList();

        foreach (var implementation in handlerImplementations)
        {
            services.AddTransient(handlerInterface, implementation);
        }

        return services;
    }
}

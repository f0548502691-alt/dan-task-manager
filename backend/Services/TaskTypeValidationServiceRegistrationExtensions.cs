using Microsoft.Extensions.DependencyInjection;

namespace DanTaskManager.Services;

public static class TaskTypeValidationServiceRegistrationExtensions
{
    public static IServiceCollection AddTaskTypeValidationServices(this IServiceCollection services)
    {
        services.AddScoped(sp => ActivatorUtilities.CreateInstance<TaskTypeValidationService>(sp));
        services.AddScoped<ITaskTypeValidationService>(sp => sp.GetRequiredService<TaskTypeValidationService>());
        services.AddScoped<ITaskTypeMetadataService>(sp => sp.GetRequiredService<TaskTypeValidationService>());

        return services;
    }
}

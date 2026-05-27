using DanTaskManager.Data;
using DanTaskManager.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DanTaskManager.Tests;

public class TaskTypeValidationServiceDependencyInjectionTests
{
    [Fact]
    public void ServiceProvider_ResolvesSharedTaskTypeValidationService_WhenBothConstructorsAreSatisfiable()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"TaskTypeValidationServiceDI-{Guid.NewGuid()}"));
        services.AddMemoryCache();
        services.Configure<TaskTypeValidationOptions>(_ => { });
        services.AddScoped<TaskTypeValidationService>();
        services.AddScoped<ITaskTypeValidationService>(sp => sp.GetRequiredService<TaskTypeValidationService>());
        services.AddScoped<ITaskTypeMetadataService>(sp => sp.GetRequiredService<TaskTypeValidationService>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureCreated();

        var concreteService = scope.ServiceProvider.GetRequiredService<TaskTypeValidationService>();
        var validationService = scope.ServiceProvider.GetRequiredService<ITaskTypeValidationService>();
        var metadataService = scope.ServiceProvider.GetRequiredService<ITaskTypeMetadataService>();

        Assert.Same(concreteService, validationService);
        Assert.Same(concreteService, metadataService);
        Assert.True(validationService.HasTaskType("Development"));
        Assert.NotNull(metadataService.GetTaskType("Development"));
    }
}

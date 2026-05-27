using DanTaskManager.Domain.Handlers;

namespace DanTaskManager.Services;

public interface ITaskTypeCatalog
{
    TaskTypeDescriptor? Find(string? taskType);
    IReadOnlyCollection<TaskTypeDescriptor> GetTaskTypes();
    IReadOnlyCollection<string> GetTaskTypeCodes();
}

public sealed record TaskTypeDescriptor(
    string TaskType,
    string DisplayName,
    int? FinalStatus,
    bool HasMetadata,
    bool HasHandler);

public sealed class TaskTypeCatalogService : ITaskTypeCatalog
{
    private readonly ITaskTypeMetadataService _metadataService;
    private readonly TaskHandlerFactory _handlerFactory;

    public TaskTypeCatalogService(
        ITaskTypeMetadataService metadataService,
        TaskHandlerFactory handlerFactory)
    {
        _metadataService = metadataService;
        _handlerFactory = handlerFactory;
    }

    public TaskTypeDescriptor? Find(string? taskType)
    {
        if (string.IsNullOrWhiteSpace(taskType))
        {
            return null;
        }

        var normalizedTaskType = taskType.Trim();
        return GetTaskTypes()
            .FirstOrDefault(type => type.TaskType.Equals(normalizedTaskType, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyCollection<TaskTypeDescriptor> GetTaskTypes()
    {
        var descriptors = new Dictionary<string, TaskTypeDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var metadataType in _metadataService.GetTaskTypes().Where(type => type.IsActive))
        {
            descriptors[metadataType.TaskType] = new TaskTypeDescriptor(
                metadataType.TaskType,
                metadataType.DisplayName,
                metadataType.FinalStatus,
                HasMetadata: true,
                HasHandler: _handlerFactory.HasHandler(metadataType.TaskType));
        }

        foreach (var handlerTaskType in _handlerFactory.GetRegisteredTaskTypes())
        {
            var handler = _handlerFactory.GetHandler(handlerTaskType);
            if (descriptors.TryGetValue(handlerTaskType, out var existing))
            {
                descriptors[handlerTaskType] = existing with
                {
                    HasHandler = true,
                    FinalStatus = existing.FinalStatus ?? handler?.FinalStatus
                };
                continue;
            }

            descriptors[handlerTaskType] = new TaskTypeDescriptor(
                handlerTaskType,
                handlerTaskType,
                handler?.FinalStatus,
                HasMetadata: false,
                HasHandler: true);
        }

        return descriptors.Values
            .OrderBy(type => type.TaskType, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyCollection<string> GetTaskTypeCodes()
    {
        return GetTaskTypes()
            .Select(type => type.TaskType)
            .ToArray();
    }
}

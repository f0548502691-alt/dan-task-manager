namespace DanTaskManager.Domain.Handlers;

/// <summary>
/// DI-facing lookup that maps a task-type code to its registered
/// <see cref="ITaskHandler"/>. Backed by a case-insensitive dictionary built
/// once at construction; <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>
/// throws on duplicate keys, so two handlers claiming the same task type
/// would fail at DI resolution.
/// </summary>
public class TaskHandlerFactory
{
    private readonly Dictionary<string, ITaskHandler> _handlersMap;

    public TaskHandlerFactory(IEnumerable<ITaskHandler> handlers)
    {
        _handlersMap = handlers.ToDictionary(
            h => h.TaskType,
            h => h,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolve the handler for <paramref name="taskType"/>, or <c>null</c>
    /// if no handler is registered for that type.
    /// </summary>
    public ITaskHandler? GetHandler(string taskType)
    {
        if (string.IsNullOrWhiteSpace(taskType))
            return null;

        _handlersMap.TryGetValue(taskType, out var handler);
        return handler;
    }

    public bool HasHandler(string taskType)
    {
        return !string.IsNullOrWhiteSpace(taskType) &&
               _handlersMap.ContainsKey(taskType);
    }

    public IEnumerable<string> GetRegisteredTaskTypes()
    {
        return _handlersMap.Keys;
    }
}

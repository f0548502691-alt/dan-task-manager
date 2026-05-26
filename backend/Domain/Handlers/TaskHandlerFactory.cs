namespace DanTaskManager.Domain.Handlers;

/// <summary>
/// Factory ל-Task Handlers
/// מזריק את כל המימושים ומחזיר את ה-Handler המתאים לפי סוג המשימה
/// </summary>
public class TaskHandlerFactory
{
    private readonly Dictionary<string, ITaskHandler> _handlersMap;

    public TaskHandlerFactory(IEnumerable<ITaskHandler> handlers)
    {
        // בנייה של מפת handlers לפי TaskType
        _handlersMap = handlers.ToDictionary(
            h => h.TaskType,
            h => h,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// קבלת Handler מתאים לפי סוג המשימה
    /// </summary>
    /// <param name="taskType">סוג המשימה</param>
    /// <returns>Handler המתאים, או null אם לא קיים</returns>
    public ITaskHandler? GetHandler(string taskType)
    {
        if (string.IsNullOrWhiteSpace(taskType))
            return null;

        _handlersMap.TryGetValue(taskType, out var handler);
        return handler;
    }

    /// <summary>
    /// בדיקה האם יש Handler עבור סוג משימה מסוים
    /// </summary>
    public bool HasHandler(string taskType)
    {
        return !string.IsNullOrWhiteSpace(taskType) &&
               _handlersMap.ContainsKey(taskType);
    }

    /// <summary>
    /// קבלת רשימה של כל סוגי ה-Handlers הרשומים
    /// </summary>
    public IEnumerable<string> GetRegisteredTaskTypes()
    {
        return _handlersMap.Keys;
    }
}

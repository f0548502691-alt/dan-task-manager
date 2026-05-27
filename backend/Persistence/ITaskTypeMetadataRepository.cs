using DanTaskManager.Domain;

namespace DanTaskManager.Persistence;

public interface ITaskTypeMetadataRepository
{
    IReadOnlyList<TaskTypeMetadata> GetAll();
    TaskTypeMetadata? GetByCode(string code, bool activeOnly = false, bool asNoTracking = false);
    TaskTypeMetadata? GetById(int id, bool asNoTracking = false);
    void Add(TaskTypeMetadata taskType);
    void SaveChanges();
}

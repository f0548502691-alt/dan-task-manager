using DanTaskManager.Data;
using DanTaskManager.Domain;
using Microsoft.EntityFrameworkCore;

namespace DanTaskManager.Persistence;

public class EfTaskTypeMetadataRepository : ITaskTypeMetadataRepository
{
    private readonly ApplicationDbContext _context;

    public EfTaskTypeMetadataRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public IReadOnlyList<TaskTypeMetadata> GetAll()
    {
        return _context.TaskTypes
            .AsNoTracking()
            .Include(taskType => taskType.FieldDefinitions)
            .OrderBy(taskType => taskType.Code)
            .ToList();
    }

    public TaskTypeMetadata? GetByCode(
        string code,
        bool activeOnly = false,
        bool asNoTracking = false)
    {
        var normalizedCode = code.Trim().ToLowerInvariant();
        var query = _context.TaskTypes
            .Include(taskType => taskType.FieldDefinitions)
            .AsQueryable();

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (activeOnly)
        {
            query = query.Where(taskType => taskType.IsActive);
        }

        return query.FirstOrDefault(taskType => taskType.Code.ToLower() == normalizedCode);
    }

    public TaskTypeMetadata? GetById(int id, bool asNoTracking = false)
    {
        var query = _context.TaskTypes
            .Include(taskType => taskType.FieldDefinitions)
            .AsQueryable();

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefault(taskType => taskType.Id == id);
    }

    public void Add(TaskTypeMetadata taskType)
    {
        _context.TaskTypes.Add(taskType);
    }

    public void SaveChanges()
    {
        _context.SaveChanges();
    }
}

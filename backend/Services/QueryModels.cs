namespace DanTaskManager.Services;

public record PageRequest(int Page = 1, int PageSize = 20)
{
    public const int MaxPageSize = 100;

    public int NormalizedPage => Page < 1 ? 1 : Page;
    public int NormalizedPageSize => PageSize < 1 ? 20 : Math.Min(PageSize, MaxPageSize);
    public int Skip => (NormalizedPage - 1) * NormalizedPageSize;
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }

    public static PagedResult<T> Create(
        IReadOnlyList<T> items,
        int totalCount,
        int page,
        int pageSize)
    {
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }
}

public class UserBriefDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public class TaskSummaryDto
{
    public int Id { get; init; }
    public string TaskType { get; init; } = string.Empty;
    public int CurrentStatus { get; init; }
    public int AssignedToUserId { get; init; }
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public UserBriefDto? AssignedToUser { get; init; }
}

public class TaskDetailsDto : TaskSummaryDto
{
    public string CustomDataJson { get; init; } = "{}";
}

public class UserSummaryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public int TasksCount { get; init; }
    public int OpenTasksCount { get; init; }
}

public class UserDetailsDto : UserSummaryDto
{
}

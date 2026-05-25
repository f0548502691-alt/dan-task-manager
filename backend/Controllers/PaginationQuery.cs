using DanTaskManager.Services;

namespace DanTaskManager.Controllers;

public class PaginationQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public PageRequest ToPageRequest() => new(Page, PageSize);
}

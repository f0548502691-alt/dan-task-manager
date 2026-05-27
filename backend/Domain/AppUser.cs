namespace DanTaskManager.Domain;

/// <summary>
/// User of the system. Tasks reference users via <see cref="BaseTask.AssignedToUserId"/>.
/// </summary>
public class AppUser
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<BaseTask> Tasks { get; set; } = new List<BaseTask>();
}

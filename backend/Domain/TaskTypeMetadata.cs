namespace DanTaskManager.Domain;

/// <summary>
/// Metadata describing a task type and the rules common to every task of that
/// type. The metadata-driven rule provider reads these rows to validate tasks
/// without requiring any C# changes when a new type is added.
/// </summary>
public class TaskTypeMetadata
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int? FinalStatus { get; set; }
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TaskFieldDefinition> FieldDefinitions { get; set; } = new List<TaskFieldDefinition>();
}

/// <summary>
/// Validation rule for a single field inside <see cref="BaseTask.CustomDataJson"/>
/// for a given task type. The same row can scope to a status range via
/// <see cref="AppliesFromStatus"/> and <see cref="AppliesToStatus"/>.
/// </summary>
public class TaskFieldDefinition
{
    public int Id { get; set; }
    public int TaskTypeMetadataId { get; set; }
    public TaskTypeMetadata? TaskTypeMetadata { get; set; }

    public string FieldKey { get; set; } = string.Empty;
    public string DataType { get; set; } = "string";
    public bool IsRequired { get; set; } = true;
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public int? ArrayLength { get; set; }
    public int? MinItems { get; set; }
    public int? MaxItems { get; set; }
    public string? ElementType { get; set; }
    public string? RegexPattern { get; set; }
    public string? AllowedValuesJson { get; set; }
    public int? AppliesFromStatus { get; set; }
    public int? AppliesToStatus { get; set; }
    public bool IsIndexed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

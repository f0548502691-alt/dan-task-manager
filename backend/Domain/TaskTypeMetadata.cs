namespace DanTaskManager.Domain;

/// <summary>
/// Metadata של סוג משימה כולל חוקיות כללית.
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
/// חוקיות של שדה מותאם אישית עבור סוג משימה.
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

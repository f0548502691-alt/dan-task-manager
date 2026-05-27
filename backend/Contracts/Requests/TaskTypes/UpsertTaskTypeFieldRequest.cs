namespace DanTaskManager.Contracts.Requests.TaskTypes;

public record UpsertTaskTypeFieldRequest(
    string Field,
    string Type = "string",
    bool Required = true,
    int? MinLength = null,
    int? MaxLength = null,
    decimal? MinValue = null,
    decimal? MaxValue = null,
    int? ArrayLength = null,
    int? MinItems = null,
    int? MaxItems = null,
    string? ElementType = null,
    string? Pattern = null,
    int? AppliesFromStatus = null,
    int? AppliesToStatus = null,
    bool AppliesOnClose = false,
    List<string>? AllowedValues = null,
    bool IsIndexed = false);

namespace DanTaskManager.Contracts.Requests.TaskTypes;

public record UpsertTaskTypeRequest(
    string TaskType,
    string? DisplayName,
    int? FinalStatus,
    bool IsActive = true);

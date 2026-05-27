# Task Type Strategy and Catalog

The backend supports task type extensibility through two cooperating mechanisms:

1. Database metadata for declarative task types and field validation.
2. Code handlers for task types that require custom C# validation.

`TaskTypeCatalogService` is the public source of supported task types for task
creation and task type schema reads.

## Architecture

```text
TaskApplicationService.CreateAsync
        |
        v
ITaskTypeCatalog
        |
        +-- active TaskTypes metadata
        |
        +-- TaskHandlerFactory registered handlers

TaskWorkflowService.ChangeStatusAsync
        |
        v
ITaskWorkflowRuleProvider ordered by Priority
        |
        +-- MetadataTaskWorkflowRuleProvider (0)
        |
        +-- HandlerTaskWorkflowRuleProvider (100)
```

## Catalog behavior

`TaskTypeCatalogService.GetTaskTypes()` builds descriptors as follows:

- Start with active metadata rows from `ITaskTypeMetadataService`.
- Add all registered handler task types from `TaskHandlerFactory`.
- If both sources define the same task type, keep the metadata display name and
  field schema, mark `HasHandler = true`, and use metadata `FinalStatus` unless
  it is missing.
- Return descriptors ordered by task type, case-insensitively.

`TaskApplicationService.CreateAsync()` rejects a task type that the catalog cannot
find and includes `supportedTaskTypes` in the error response.

## Handler registration

`AddTaskHandlersFromAssembly()` scans the backend assembly for public,
non-abstract types assignable to `IRegisterableTaskHandler` and registers them as
`ITaskHandler`.

This marker matters:

- Implement `IRegisterableTaskHandler` for code-backed task types that should be
  public and creatable.
- Implement only `ITaskHandler` for reusable validation classes that should not
  be discovered as supported task types.
- Metadata-backed task types do not need handlers.

Current registerable handlers:

| Handler | Task type | Final status | Required data |
|---------|-----------|--------------|---------------|
| `AnalysisTaskHandler` | `Analysis` | `2` | Status `2`: non-empty `analysisReport`. |
| `TestingTaskHandler` | `Testing` | `3` | Status `2`: `testCases` integer greater than 0. Status `3`: percentage `coverage` and non-empty `summary`. |

`ProcurementTaskHandler` and `DevelopmentTaskHandler` still implement
`ITaskHandler`, but are not registerable. Their public behavior is supplied by
seeded metadata and the metadata rule provider.

## Workflow rule strategy

`TaskWorkflowService` does not know field names or task-type-specific validation
rules. It resolves a provider by calling `CanHandle(taskType)` in priority order.

| Provider | Use case | Validation source |
|----------|----------|-------------------|
| `MetadataTaskWorkflowRuleProvider` | Declarative task types in `TaskTypes` | `TaskTypeValidationService.ValidateStatusData()` |
| `HandlerTaskWorkflowRuleProvider` | Code-backed handler types | `ITaskHandler.ValidateStatusChange()` |

Close behavior is shared through `WorkflowCloseData.Merge()`, which preserves the
current JSON object when possible and adds `finalNotes` plus `closedAt`.

## Adding a code strategy

```csharp
public sealed class AuditTaskHandler : IRegisterableTaskHandler
{
    public string TaskType => "Audit";
    public int FinalStatus => 2;

    public ValidationResult ValidateStatusChange(
        string currentDataJson,
        int currentStatus,
        int nextStatus,
        string newDataJson)
    {
        if (currentStatus >= FinalStatus && nextStatus > currentStatus)
        {
            return ValidationResult.Failure("Cannot advance Audit task beyond final status 2");
        }

        return nextStatus == 2
            ? ValidateAuditPayload(newDataJson)
            : ValidationResult.Success();
    }
}
```

After the class is added to the backend assembly, the catalog exposes `Audit`.
If the Angular client receives no fields for it, it uses the fallback JSON editor
for workflow payloads.

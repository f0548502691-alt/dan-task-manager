# Workflow strategy and extension architecture

Dan Task Manager uses a provider/strategy model to keep generic workflow rules
separate from task-type validation.

## Current design

```text
TasksController
  -> MediatR command/query handlers
  -> TaskApplicationService
  -> TaskWorkflowService
      -> ITaskWorkflowRuleProvider[] ordered by Priority
          0   MetadataTaskWorkflowRuleProvider
          100 HandlerTaskWorkflowRuleProvider
```

`TaskWorkflowService` owns rules that apply to every task:

- task must exist
- closed tasks are immutable
- next assignee must exist
- `customFields` must serialize to a JSON object
- forward movement is exactly `+1`
- backward movement can target a lower status
- status `99` can only be reached through `CloseTaskAsync`
- close is allowed only from the task type final status

`ITaskWorkflowRuleProvider` owns task-type-specific validation:

```csharp
public interface ITaskWorkflowRuleProvider
{
    int Priority { get; }
    bool CanHandle(string taskType);
    int? GetFinalStatus(string taskType);
    ValidationResult ValidateStatusChange(BaseTask task, int nextStatus, string newDataJson);
}
```

## Provider order

| Provider | Priority | Data source | Purpose |
|----------|----------|-------------|---------|
| `MetadataTaskWorkflowRuleProvider` | `0` | `TaskTypeValidationService` and database metadata | Runtime field rules and final status for supported task types |
| `HandlerTaskWorkflowRuleProvider` | `100` | `TaskHandlerFactory` and `ITaskHandler` implementations | Fallback hard-coded rules |

The first provider whose `CanHandle(taskType)` returns true wins. This means
database metadata overrides handler rules when both exist.

## Built-in handler fallback

Only `ProcurementTaskHandler` and `DevelopmentTaskHandler` are registered in
`Program.cs`.

| Handler | `TaskType` | `FinalStatus` | Rules |
|---------|------------|---------------|-------|
| `ProcurementTaskHandler` | `Procurement` | `3` | status 2 requires `prices` array of exactly two non-empty strings; status 3 requires non-empty `receipt` string |
| `DevelopmentTaskHandler` | `Development` | `4` | status 2 requires `specification` with at least 10 chars; status 3 requires valid `branchName`; status 4 requires `versionNumber` string or number |

`WorkflowConstants.SupportedTaskTypes` gates support before providers run:

```csharp
public static readonly string[] SupportedTaskTypes =
{
    "Procurement",
    "Development"
};
```

Adding metadata alone is not enough for a new public task type. The allow-list,
backend validation, tests, and frontend behavior must also be updated.

## Metadata-backed rules

`HybridSchemaBootstrapper.EnsureSchema` creates and seeds:

- `TaskTypes`
- `TaskFieldDefinitions`
- computed JSON indexes on `Tasks` for `priority`, `branchName`, and `deadline`
- `CK_Tasks_CustomDataJson_IsJson`

Seeded metadata mirrors the built-in handlers:

```text
Procurement finalStatus=3
  prices: array<string>, required at status 2, arrayLength=2
  receipt: string, required at status 3

Development finalStatus=4
  specification: string, required at status 2, minLength=10
  branchName: string, required at status 3, pattern=valid_git_branch
  versionNumber: stringOrNumber, required at status 4, pattern=semantic_version
```

Supported field rule types are implemented in `TaskTypeValidationService`.
Before documenting or adding a new rule type, verify it there.

## Adding or changing task behavior

### Change rules for an existing supported task type

1. Prefer metadata when the rule can be expressed by `TaskFieldDefinitions`.
2. Keep handler fallback aligned if the application must work before metadata is
   present.
3. Update tests covering `TaskTypeValidationService`, handlers, and workflow
   movement.
4. Update frontend labels/adapters if the visible form fields change.

### Add a new first-class task type

1. Add the type to `WorkflowConstants.SupportedTaskTypes`.
2. Add metadata seed rows in `HybridSchemaBootstrapper` or expose creation
   through the metadata API.
3. Add an `ITaskHandler` fallback when metadata is not sufficient or when startup
   without metadata should still validate the type.
4. Register the handler in `Program.cs` or deliberately adopt assembly scanning
   via `TaskHandlerRegistrationExtensions`.
5. Add backend tests for create, status movement, metadata validation, fallback
   validation, close behavior, and unsupported-type errors.
6. Add frontend labels, final-status defaults, adapters, and form components when
   the type needs a first-class UI. Otherwise the fallback JSON editor is used.

## API boundary constraints

- Controllers accept public request DTOs from `Contracts/Requests`.
- Public custom data is named `customFields`.
- `BaseTask.CustomDataJson` is internal persistence.
- List endpoints use `TaskSummaryDto` and omit custom fields.
- Detail/create/status/close responses use `TaskDetailsDto` and include
  `customFields`.

Keep extension docs and examples aligned with these names; legacy
`customDataJson` and `newDataJson` should not appear in public API examples.

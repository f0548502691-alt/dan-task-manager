# Workflow rule providers and task handlers

The workflow layer uses a provider chain to select validation rules for a task
type. Handlers still exist, but they are no longer the only source of workflow
rules.

## Architecture

```text
TasksController
    -> TaskApplicationService
        -> TaskWorkflowService
            -> ITaskWorkflowRuleProvider list ordered by Priority
                1. MetadataTaskWorkflowRuleProvider (Priority 0)
                2. HandlerTaskWorkflowRuleProvider  (Priority 100)
```

`TaskWorkflowService` owns generic workflow invariants:

- task must exist
- task must not be closed
- `nextAssignedToUserId` must refer to an existing user
- `customFields` must serialize to a JSON object
- forward movement is exactly `+1`
- rollback can move to any lower status down to `1`
- closing is handled only by `CloseTaskAsync`
- task cannot move forward after reaching final status

Rule providers own task-type-specific requirements such as `prices`,
`receipt`, `specification`, `branchName`, and `versionNumber`.

## Supported task types

The current product contract supports only:

```csharp
public static readonly string[] SupportedTaskTypes =
{
    "Procurement",
    "Development"
};
```

This allow-list is checked before task creation, workflow rule provider
resolution, and task-type metadata writes. Adding a handler or metadata row for
another type is not enough; the type must also be added to
`WorkflowConstants.SupportedTaskTypes` and the frontend workflow board.

## Provider precedence

### MetadataTaskWorkflowRuleProvider

- Priority: `0`
- Source: `TaskTypeValidationService`
- Handles a task type when active metadata exists in `TaskTypes`.
- Uses `TaskFieldDefinitions` to validate status-specific fields.
- Supplies metadata final status when present.

### HandlerTaskWorkflowRuleProvider

- Priority: `100`
- Source: `TaskHandlerFactory`
- Fallback when metadata does not handle the supported task type.
- Uses explicitly registered handlers from `Program.cs`.

```csharp
builder.Services.AddTransient<ITaskHandler, ProcurementTaskHandler>();
builder.Services.AddTransient<ITaskHandler, DevelopmentTaskHandler>();
```

`TaskWorkflowService` orders providers by ascending `Priority` and uses the
first provider whose `CanHandle(taskType)` returns `true`.

## Current type rules

| Task type | Final status | Status | Required fields |
| --- | ---: | ---: | --- |
| `Procurement` | `3` | `2` | `prices`: array, exactly two strings |
| `Procurement` | `3` | `3` | `receipt`: non-empty string |
| `Development` | `4` | `2` | `specification`: string, minimum 10 characters |
| `Development` | `4` | `3` | `branchName`: valid git branch string |
| `Development` | `4` | `4` | `versionNumber`: semantic version string/number |

## Public API payloads

The public API uses `customFields` objects. Older examples using `newDataJson`
or `customDataJson` describe internal storage or stale request contracts.

```json
{
  "newStatus": 2,
  "nextAssignedToUserId": 2,
  "customFields": {
    "prices": ["5000", "4800"]
  }
}
```

List endpoints return paged summaries and omit `customFields`; clients should
load `GET /api/tasks/{id}` before hydrating type-specific edit forms.

## Extending the strategy

When adding a new supported type:

1. Add the type to `WorkflowConstants.SupportedTaskTypes`.
2. Register a fallback `ITaskHandler` in `Program.cs`.
3. Seed or migrate task-type metadata and field definitions.
4. Add backend tests for supported/unsupported type behavior and field
   validation.
5. Update frontend status/final-status mappings and payload adapters.

When adding a new source of workflow rules, implement
`ITaskWorkflowRuleProvider` with an explicit `Priority` and register it in DI.
Keep generic movement and persistence rules inside `TaskWorkflowService` so rule
providers stay focused on type-specific validation.

# Extension guide

Use this guide when changing workflow behavior, adding API surface, or extending
task metadata. Verify any behavioral change against source and tests before
updating docs.

## Current extension points

| Extension point | Primary files | Notes |
|-----------------|---------------|-------|
| Task workflow rules | `TaskWorkflowService.cs`, `TaskWorkflowRuleProviders.cs` | Generic movement rules stay in the workflow service; task-type rules stay in providers |
| Metadata field validation | `TaskTypeValidationService.cs`, `HybridSchemaBootstrapper.cs` | Preferred path for supported task type field requirements |
| Handler fallback | `Domain/Handlers/*.cs`, `Program.cs` | Used when metadata cannot handle a supported task type |
| Public request models | `Contracts/Requests/**` | Controllers should bind these DTOs, not domain entities |
| Query DTOs | `Services/QueryModels.cs`, `TaskDtoMappings.cs` | Keep list summaries light and EF-translatable |
| Frontend forms | `frontend/src/app/tasks/*` | Add labels/adapters/components for first-class task type UI |

## Add or change validation for an existing task type

Prefer metadata when a rule is field-based.

1. Add or update seed data in `HybridSchemaBootstrapper.EnsureSchema`, or use the
   task type metadata endpoints at runtime.
2. Confirm the task type is still listed in `WorkflowConstants.SupportedTaskTypes`.
3. Keep handler fallback behavior aligned when startup without metadata should
   still enforce the rule.
4. Add tests for the metadata service and workflow status change path.
5. Update `WORKFLOW_SERVICE_DOCS.md` and frontend adapters if the public payload
   changes.

Example metadata field rule request:

```http
POST /api/task-types/Procurement/fields
Content-Type: application/json

{
  "field": "prices",
  "type": "array",
  "required": true,
  "arrayLength": 2,
  "elementType": "string",
  "appliesFromStatus": 2,
  "appliesToStatus": 2
}
```

## Add a new task type

Adding metadata alone is not enough because the workflow service rejects task
types outside `WorkflowConstants.SupportedTaskTypes`.

Checklist:

1. Add the type to `WorkflowConstants.SupportedTaskTypes`.
2. Add metadata seed rows for `TaskTypes` and `TaskFieldDefinitions`.
3. Add an `ITaskHandler` fallback if metadata is unavailable or insufficient.
4. Register the handler in `Program.cs`, or explicitly switch to assembly
   scanning with `TaskHandlerRegistrationExtensions`.
5. Add backend tests for:
   - create success
   - unsupported type failure
   - forward and backward movement
   - invalid field payload
   - close only from final status
   - closed-task immutability
6. Update frontend:
   - `TASK_STATUS_LABELS_BY_TYPE`
   - `DEFAULT_TASK_FINAL_STATUS_BY_TYPE` if metadata fallback is needed
   - `task-workflow-adapters.ts`
   - a field component when JSON editing is not enough
7. Update docs with the new payload examples and constraints.

Minimal handler shape:

```csharp
public class QaTaskHandler : StatusValidationTaskHandlerBase
{
    public QaTaskHandler()
        : base(new Dictionary<int, Func<string, ValidationResult>>
        {
            [2] = ValidateStatusTwo
        })
    {
    }

    public string TaskType => "QA";
    public int FinalStatus => 2;

    private static ValidationResult ValidateStatusTwo(string newDataJson)
    {
        using var json = JsonDocument.Parse(newDataJson);
        return json.RootElement.TryGetProperty("testPlan", out var value) &&
               value.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(value.GetString())
            ? ValidationResult.Success()
            : ValidationResult.Failure("Status 2 requires a non-empty testPlan");
    }
}
```

Public API clients would still send:

```json
{
  "newStatus": 2,
  "nextAssignedToUserId": 3,
  "customFields": {
    "testPlan": "Run regression suite"
  }
}
```

`newDataJson` is an internal service/handler parameter name, not a public request
property.

## Add a new endpoint

1. Add or reuse a request DTO under `Contracts/Requests`.
2. Add FluentValidation rules when the endpoint accepts input.
3. Add a MediatR command/query handler for controller-to-service delegation.
4. Keep business rules in an application service or workflow service, not in the
   controller.
5. Throw `ApiValidationException`, `WorkflowValidationException`, or
   `ApiNotFoundException` so `GlobalExceptionMiddleware` can emit consistent
   `{ error, code }` responses.
6. Add tests for success and failure cases.

## Extend list DTOs safely

`TaskDtoMappings.ToTaskSummary()` is an EF expression projection. When adding
fields to `TaskSummaryDto`:

- keep the projection translatable by EF Core;
- avoid adding `customFields` to list responses without checking payload size and
  frontend assumptions;
- update both `TaskApplicationService` and `UserApplicationService` paths by
  changing the shared mapping helper once.

Use `TaskDetailsDto` for fields that are only needed after selecting a task.

## Frontend extension notes

The frontend is zoneless and standalone. When adding workflow UI:

- use signals or reactive forms for state;
- clean subscriptions with `takeUntilDestroyed`;
- send public `customFields` objects;
- keep fallback JSON editor behavior for unknown metadata task types;
- preserve `TaskService` response normalization for paged task lists.

## Documentation requirements for extensions

Update docs in the same change when you alter public behavior:

- [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md) for workflow/API payloads
- [API_ERROR_CODES.md](API_ERROR_CODES.md) for new error codes or messages
- [STRATEGY_PATTERN_DOCS.md](STRATEGY_PATTERN_DOCS.md) for architecture changes
- [../frontend/README.md](../frontend/README.md) for client workflow changes

# Extension Guide

DanTaskManager is designed so most task-type changes are added without editing
the workflow engine. The central rule is:

- Use metadata for task types whose status rules can be expressed as field
  definitions.
- Use a code-backed handler only when validation needs custom logic that the
  metadata model cannot represent.

The general workflow invariants live in `Services/TaskWorkflowService.cs`.
Per-type rules come from ordered `ITaskWorkflowRuleProvider` implementations:

| Provider | Priority | Source |
|----------|----------|--------|
| `MetadataTaskWorkflowRuleProvider` | `0` | `TaskTypeMetadata` and `TaskFieldDefinition` rows |
| `HandlerTaskWorkflowRuleProvider` | `100` | DI-discovered `IRegisterableTaskHandler` classes |

Lower priority wins. If metadata and a handler claim the same task type,
metadata handles the type at runtime. `TaskTypeConflictValidator` logs the
overlap at startup, or fails startup when
`TaskTypeConflictValidation:FailOnConflict` is `true`.

## Supported field-rule model

Metadata-backed task types use `TaskFieldDefinition` rows. The API exposes the
same model through `POST /api/task-types/{taskType}/fields` and
`PUT /api/task-types/{taskType}/fields/{field}`.

| Field | Purpose |
|-------|---------|
| `FieldKey` / `field` | JSON property inside request `customFields`. |
| `DataType` / `type` | `string`, `number`, `array`, `object`, `boolean`, or `stringOrNumber`. |
| `IsRequired` / `required` | Whether the field must exist for the matching status. |
| `MinLength`, `MaxLength` | String constraints. |
| `MinValue`, `MaxValue` | Numeric constraints. |
| `ArrayLength`, `MinItems`, `MaxItems`, `ElementType` | Array constraints. |
| `RegexPattern` / `pattern` | Named pattern (`valid_git_branch`, `semantic_version`) or a regular expression. |
| `AllowedValuesJson` / `allowedValues` | Enum-like list for string values. |
| `AppliesFromStatus`, `AppliesToStatus` | Status range where the rule applies. |
| `IsIndexed` / `isIndexed` | Requests a SQL Server computed column + index for scalar custom fields. |

`JsonIndexBootstrapper` materializes indexes at API startup for scalar fields
marked `IsIndexed = true`. Arrays and objects are intentionally skipped.

## Add a metadata-backed task type

Use this path when validation is declarative.

### 1. Create the task type

```bash
curl -X POST http://localhost:8080/api/task-types \
  -H "Content-Type: application/json" \
  -d '{
    "taskType": "Legal",
    "displayName": "Legal Review",
    "finalStatus": 3,
    "isActive": true
  }'
```

Constraints:

- `finalStatus` must be at least created status `1`.
- `finalStatus` must be less than closed status `99`.
- Task-type lookup is case-insensitive, but clients should use the canonical
  `taskType` returned by `GET /api/task-types`.

### 2. Add status field rules

Status `2` requires a document URL:

```bash
curl -X POST http://localhost:8080/api/task-types/Legal/fields \
  -H "Content-Type: application/json" \
  -d '{
    "field": "documentUrl",
    "type": "string",
    "required": true,
    "minLength": 10,
    "appliesFromStatus": 2,
    "appliesToStatus": 2,
    "isIndexed": false
  }'
```

Status `3` requires a decision from a fixed set:

```bash
curl -X POST http://localhost:8080/api/task-types/Legal/fields \
  -H "Content-Type: application/json" \
  -d '{
    "field": "decision",
    "type": "string",
    "required": true,
    "allowedValues": ["Approved", "Rejected", "NeedsChanges"],
    "appliesFromStatus": 3,
    "appliesToStatus": 3,
    "isIndexed": true
  }'
```

### 3. Verify the schema

```bash
curl http://localhost:8080/api/task-types/Legal
```

The frontend reads `GET /api/task-types` and renders dynamic fields from this
schema. Unknown task types can still use the fallback JSON editor, but the best
developer experience comes from complete metadata.

### 4. Exercise the workflow

```bash
curl -X POST http://localhost:8080/api/tasks \
  -H "Content-Type: application/json" \
  -d '{
    "taskType": "Legal",
    "description": "Review vendor contract",
    "assignedToUserId": 1,
    "customFields": {}
  }'
```

Move from status `1` to `2`:

```bash
curl -X POST http://localhost:8080/api/tasks/3/change-status \
  -H "Content-Type: application/json" \
  -d '{
    "newStatus": 2,
    "nextAssignedToUserId": 2,
    "customFields": {
      "documentUrl": "https://example.test/contracts/42"
    }
  }'
```

Then move from status `2` to `3`, and close through
`POST /api/tasks/{id}/close`. Do not move directly to status `99` with
`change-status`.

## Seed a metadata-backed task type in code

For demo data or baseline product task types, add rows in
`Data/ApplicationDbContext.SeedData`.

1. Add one `TaskTypeMetadata` row with a stable Id, code, display name, final
   status, active flag, version, and deterministic timestamps.
2. Add one `TaskFieldDefinition` row per status field rule.
3. If the schema is already deployed, create a migration:

   ```bash
   cd backend
   dotnet ef migrations add AddLegalTaskType
   ```

4. Commit the generated migration and
   `Migrations/ApplicationDbContextModelSnapshot.cs`.

The current initial migration demonstrates this pattern for `Procurement`,
`Development`, and `Marketing`.

## Add a code-backed task type

Use this path when validation requires custom parsing, external checks, or logic
that does not fit the field-rule model.

### 1. Implement `IRegisterableTaskHandler`

```csharp
using System.Text.Json;

namespace DanTaskManager.Domain.Handlers;

public sealed class SecurityReviewTaskHandler : IRegisterableTaskHandler
{
    public string TaskType => "SecurityReview";

    public int FinalStatus => 2;

    public ValidationResult ValidateStatusChange(
        string currentDataJson,
        int currentStatus,
        int nextStatus,
        string newDataJson)
    {
        if (nextStatus != 2)
        {
            return ValidationResult.Success();
        }

        try
        {
            using var document = JsonDocument.Parse(newDataJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("riskLevel", out var riskLevel) ||
                riskLevel.ValueKind != JsonValueKind.String)
            {
                return ValidationResult.Failure("Status 2 requires a 'riskLevel' field");
            }

            var value = riskLevel.GetString();
            return value is "Low" or "Medium" or "High"
                ? ValidationResult.Success()
                : ValidationResult.Failure("'riskLevel' must be Low, Medium, or High");
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"Invalid JSON payload: {ex.Message}");
        }
    }
}
```

### 2. Let DI discover it

No manual registration is needed. `Program.cs` calls:

```csharp
builder.Services.AddTaskHandlersFromAssembly(typeof(Program).Assembly);
```

`TaskHandlerRegistrationExtensions` registers every public, non-abstract class
that implements `IRegisterableTaskHandler`.

### 3. Add tests

Cover both the handler and the application workflow:

- Handler unit tests for every status-specific rule.
- `TaskWorkflowService` tests for movement and close behavior.
- API or MediatR handler tests when request/response behavior changes.

### 4. Consider frontend metadata

`GET /api/task-types` returns handler-backed task types with an empty `fields`
array. The frontend can create and move them through the fallback JSON editor,
but it cannot render guided controls unless equivalent metadata exists. If the
logic is handler-backed but the UI still needs field hints, add inactive or
non-conflicting metadata carefully and verify the conflict behavior.

## Add a new endpoint

Follow the existing application structure:

1. Add request/response contracts under `Contracts/Requests` or a service DTO in
   `Services/QueryModels.cs`.
2. Add a MediatR command/query under `Application/Tasks/<UseCase>`.
3. Keep controller methods thin: validate the HTTP request, send the command or
   query, and translate not-found/validation failures through `ApiException`
   types.
4. Put business rules in services, not controllers.
5. Add tests for the handler/service and any controller-specific validation.

Use stable error `code` values for new failures. See `docs/API_ERROR_CODES.md`.

## Add database fields or tables

1. Update the domain entity and `ApplicationDbContext` configuration.
2. Add or update seed data only when it should exist in every environment.
3. Generate a migration from `/backend`.
4. Review generated SQL for constraints, indexes, cascade behavior, and data
   backfills.
5. Commit the migration and model snapshot.

Startup applies migrations automatically when migrations exist, but production
deployments should still treat migrations as an explicit release step.

## Common pitfalls

| Pitfall | Avoidance |
|---------|-----------|
| Adding a handler for a metadata-backed task type | Metadata has priority `0`; the handler is shadowed. Use one source or enable `TaskTypeConflictValidation:FailOnConflict`. |
| Moving to status `99` through `change-status` | Use `POST /api/tasks/{id}/close`; closed status is not a normal transition. |
| Sending `customDataJson` or `newDataJson` from clients | Public requests use `customFields`. `CustomDataJson` is internal storage. |
| Creating another initial migration | The initial migration already exists. Generate migrations only for model changes. |
| Marking arrays or objects as indexed | `JsonIndexBootstrapper` only materializes scalar `string`, `number`, and `stringOrNumber` fields. |
| Forgetting `nextAssignedToUserId` | Status changes and close requests require the next assignee to exist. |

## Related docs

- `docs/WORKFLOW.md` - workflow invariants, providers, and status constants.
- `docs/QUICKSTART.md` - setup, migration, seed, and smoke-test runbook.
- `docs/API_ERROR_CODES.md` - stable error-code contract.
- `docs/BEST_PRACTICES.md` - local coding conventions.

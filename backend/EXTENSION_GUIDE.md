# Extension Guide

Use metadata for most new task types. Add code handlers only when validation
cannot be expressed with field rules.

## Choose an extension path

| Need | Recommended path |
|------|------------------|
| New task type with required fields, scalar/array constraints, allowed values, or regex validation | Add task type metadata and field definitions. |
| New task type that must run custom C# validation | Add an `IRegisterableTaskHandler`. |
| New public workflow operation | Add a request model, validator, MediatR command/query, application-service method, and controller action. |
| New field shape rendered by the Angular client | Extend `task-workflow-schema.ts` and `TaskDynamicFieldsComponent`. |

## Add a metadata-backed task type

1. Create or update the task type:

```http
POST /api/task-types
Content-Type: application/json

{
  "taskType": "Release",
  "displayName": "Release",
  "finalStatus": 3,
  "isActive": true
}
```

Constraints:

- `taskType` is required and compared case-insensitively.
- `finalStatus` is required for DB-backed metadata updates.
- `finalStatus` must be greater than or equal to `1` and less than `99`.
- Updating existing metadata increments `version`.

2. Add field rules:

```http
PUT /api/task-types/Release/fields/releaseVersion
Content-Type: application/json

{
  "type": "stringOrNumber",
  "required": true,
  "pattern": "semantic_version",
  "appliesFromStatus": 2,
  "appliesToStatus": 2,
  "isIndexed": false
}
```

```http
PUT /api/task-types/Release/fields/approval
Content-Type: application/json

{
  "type": "string",
  "required": true,
  "allowedValues": ["approved", "rejected"],
  "appliesFromStatus": 3,
  "appliesToStatus": 3
}
```

3. Verify the catalog:

```http
GET /api/task-types/Release
GET /api/task-types/Release/schema
GET /api/task-types
```

4. Create and advance a task:

```http
POST /api/tasks
Content-Type: application/json

{
  "taskType": "Release",
  "description": "Publish package",
  "assignedToUserId": 1,
  "customFields": {}
}
```

```http
POST /api/tasks/42/change-status
Content-Type: application/json

{
  "newStatus": 2,
  "nextAssignedToUserId": 2,
  "customFields": {
    "releaseVersion": "1.0.0"
  }
}
```

## Add a code-backed task type

Implement `IRegisterableTaskHandler`. `AddTaskHandlersFromAssembly()` registers
only classes assignable to this marker interface, so plain `ITaskHandler`
implementations are available to tests or internal composition but are not
published as supported task types.

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
        if (currentStatus >= FinalStatus && nextStatus > currentStatus)
        {
            return ValidationResult.Failure(
                $"Cannot advance {TaskType} task beyond final status {FinalStatus}");
        }

        if (nextStatus != 2)
        {
            return ValidationResult.Success();
        }

        using var document = JsonDocument.Parse(newDataJson);
        var root = document.RootElement;

        return root.TryGetProperty("riskAccepted", out var riskAccepted) &&
               (riskAccepted.ValueKind == JsonValueKind.True || riskAccepted.ValueKind == JsonValueKind.False)
            ? ValidationResult.Success()
            : ValidationResult.Failure("Status 2 requires a boolean 'riskAccepted' field");
    }
}
```

No manual DI registration is needed if the handler is public, non-abstract, and in
the backend assembly. The catalog will expose it with an empty field schema:

```json
{
  "taskType": "SecurityReview",
  "displayName": "SecurityReview",
  "finalStatus": 2,
  "isActive": true,
  "version": 1,
  "fields": []
}
```

Because there are no schema fields, the current Angular workflow UI falls back to
the JSON payload editor for this task type.

## Add frontend field support

The frontend renders metadata fields through `TaskDynamicFieldsComponent` and
helpers in `task-workflow-schema.ts`.

When adding a new metadata rule:

- Prefer existing types: `string`, `number`, `array`, `object`, `boolean`,
  `stringOrNumber`.
- Keep `arrayLength` or `minItems`/`maxItems` explicit when the UI must know how
  many controls to render.
- Use `appliesFromStatus` and `appliesToStatus` so only relevant controls are
  required for the selected target status.
- Add client-side support before relying on new field types beyond text, number,
  textarea, and fixed-length arrays.

The backend remains authoritative; client validators only improve UX.

## Testing checklist

- Unit-test new handlers directly when using `IRegisterableTaskHandler`.
- Add application-service or workflow tests for catalog behavior and unsupported
  task types.
- Exercise `GET /api/task-types` for metadata-backed types so the frontend can
  render the schema.
- Verify create/status/close with public `customFields` payloads, not internal
  `CustomDataJson`.
- Run `dotnet test backend/DanTaskManager.csproj` and, for frontend-impacting
  schema changes, `npm --prefix frontend test`.

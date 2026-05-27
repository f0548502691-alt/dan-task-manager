# Dan Task Manager Backend

.NET 8 API for task workflow management. Tasks share one persistence model (`BaseTask`)
and vary by task type through metadata-backed field rules or code-backed handlers.

## Current architecture

```text
TasksController / TaskTypesController
        |
MediatR commands and queries
        |
TaskApplicationService / TaskWorkflowService
        |
TaskTypeCatalog + workflow rule providers
        |
EF Core SQL Server tables
```

Key components:

- `Domain/BaseTask.cs` stores common task columns and `CustomDataJson`.
- `Domain/TaskTypeMetadata.cs` stores task type definitions and field rules.
- `Services/TaskTypeCatalogService.cs` exposes every supported task type by merging
  active database metadata with registerable handlers.
- `Services/TaskTypeValidationService.cs` validates metadata-backed `customFields`
  and serves task type schemas.
- `Services/TaskWorkflowService.cs` enforces movement rules, final status rules,
  assignee changes, and closing.
- `Services/TaskProjectionExpressions.cs` contains the shared EF projection for
  paged task summary responses.

## Task type model

Supported task types no longer come from a hard-coded allow-list.

1. Active rows in `TaskTypes` are supported and validated by
   `MetadataTaskWorkflowRuleProvider` (priority `0`).
2. Classes implementing `IRegisterableTaskHandler` are auto-registered from the
   API assembly and validated by `HandlerTaskWorkflowRuleProvider` (priority `100`).
3. `TaskTypeCatalogService` merges both sources for task creation and
   `GET /api/task-types`.

Seeded metadata-backed types:

| Task type | Final status | Required status data |
|-----------|--------------|----------------------|
| `Procurement` | `3` | Status `2`: `prices` array with 2 string values. Status `3`: `receipt` string. |
| `Development` | `4` | Status `2`: `specification` string, min 10 chars. Status `3`: `branchName`. Status `4`: `versionNumber`. |

Handler-backed examples:

| Task type | Source | Notes |
|-----------|--------|-------|
| `Analysis` | `AnalysisTaskHandler : IRegisterableTaskHandler` | Status `2` requires `analysisReport`. |
| `Testing` | `TestingTaskHandler : IRegisterableTaskHandler` | Status `2` requires `testCases`; status `3` requires `coverage` and `summary`. |

Metadata-backed types do not need a handler. If a task type has both metadata and a
handler, metadata validation wins because its rule provider has the lower priority.

## Workflow constraints

- Created status is `1` (`WorkflowConstants.CreatedStatus`).
- Closed status is `99` (`WorkflowConstants.ClosedStatus`).
- Forward movement must be exactly `+1`.
- Backward movement may move to any lower status greater than or equal to `1`.
- `POST /api/tasks/{id}/change-status` requires `nextAssignedToUserId` and a
  JSON-object `customFields` payload.
- A task can be closed only from its configured final status, and only through
  `POST /api/tasks/{id}/close`.
- Closed tasks cannot be updated, deleted, or moved.

## API shape

Public request/response JSON uses `customFields`; `CustomDataJson` is an internal
storage column.

```http
POST /api/tasks
Content-Type: application/json

{
  "taskType": "Procurement",
  "description": "Collect supplier quotes",
  "assignedToUserId": 1,
  "customFields": {}
}
```

```http
POST /api/tasks/1/change-status
Content-Type: application/json

{
  "newStatus": 2,
  "nextAssignedToUserId": 2,
  "customFields": {
    "prices": ["5000", "4800"]
  }
}
```

List endpoints return `PagedResult<TaskSummaryDto>` and intentionally omit
`customFields`; fetch `GET /api/tasks/{id}` for hydrated task details.

## Task type schema API

`GET /api/task-types` returns active metadata schemas plus handler-backed schema
stubs:

```json
[
  {
    "taskType": "Procurement",
    "displayName": "Procurement",
    "finalStatus": 3,
    "isActive": true,
    "version": 1,
    "fields": [
      {
        "field": "prices",
        "type": "array",
        "required": true,
        "arrayLength": 2,
        "elementType": "string",
        "appliesFromStatus": 2,
        "appliesToStatus": 2,
        "isIndexed": false
      }
    ]
  }
]
```

Handler-backed schemas have `fields: []`, `displayName` equal to the handler
`TaskType`, and `finalStatus` from the handler.

## Local setup

```bash
dotnet restore
dotnet ef database update
dotnet run --project backend/DanTaskManager.csproj
dotnet test backend/DanTaskManager.csproj
```

At startup the app applies migrations when present, falls back to `EnsureCreated()`
otherwise, and then runs `HybridSchemaBootstrapper.EnsureSchema()` so existing
databases get the task type metadata tables and seeded field definitions.

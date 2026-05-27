# DanTaskManager backend

.NET 8 API for workflow-driven task management. The backend stores generic
tasks in SQL Server, validates status-specific payloads, and exposes REST
endpoints for creating, reading, moving, closing, updating, and deleting tasks.

## Current architecture

```
Controllers/
  TasksController.cs       HTTP task operations
  UsersController.cs       user reads and assigned-task reads
  TaskTypesController.cs   task type metadata and field schemas
Application/Tasks/         MediatR commands and queries
Services/
  TaskApplicationService.cs       task query/create/update/delete facade
  TaskWorkflowService.cs          status movement, reassignment, close rules
  TaskWorkflowRuleProviders.cs    metadata-first validation provider chain
  TaskTypeValidationService.cs    DB-backed task type schemas and field rules
  TaskTypeCatalogService.cs       active metadata + registerable handlers
Domain/
  BaseTask.cs, AppUser.cs, WorkflowConstants.cs
  Handlers/                       code-backed validation strategies
Data/
  ApplicationDbContext.cs
  HybridSchemaBootstrapper.cs     task type metadata bootstrap
```

Important constants:

- `WorkflowConstants.CreatedStatus` is `1`; new tasks never start at `0`.
- `WorkflowConstants.ClosedStatus` is `99`; closed tasks cannot be changed,
  updated, or deleted.
- Public request/response payloads use `customFields`. `CustomDataJson` is the
  internal persisted JSON column.

## Setup

1. Configure SQL Server in `appsettings.json`.

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=.;Database=DanTaskManager;Trusted_Connection=true;Encrypt=false;TrustServerCertificate=true;"
     }
   }
   ```

2. Restore and run the API.

   ```bash
   dotnet restore backend/DanTaskManager.csproj
   dotnet run --project backend/DanTaskManager.csproj
   ```

On startup `Program.cs` applies EF migrations when migrations exist, otherwise
it calls `EnsureCreated()`, then `HybridSchemaBootstrapper.EnsureSchema()` to
create the task type metadata tables and seed the current metadata-backed task
types.

## Task operation contracts

The main task operations are routed through MediatR handlers and
`TaskApplicationService`; workflow transitions delegate to
`TaskWorkflowService`.

| Operation | Endpoint | Notes |
| --- | --- | --- |
| Create task | `POST /api/tasks` | Requires `taskType`, `description`, `assignedToUserId`; optional `customFields` must be an object. Creates status `1`. |
| Get task | `GET /api/tasks/{id}` | Returns `TaskDetailsDto` with `customFields` and `assignedToUser`. |
| Change status | `POST /api/tasks/{id}/change-status` | Requires `newStatus`, `nextAssignedToUserId`, and object `customFields`. Forward movement must be exactly `+1`; rollback can move to any lower status >= `1`. |
| Close task | `POST /api/tasks/{id}/close` | Requires `nextAssignedToUserId` and `finalNotes`. The task must already be at its task type final status; close sets status `99`, reassigns the task, and merges `finalNotes`/`closedAt` into `customFields`. |

List endpoints (`GET /api/tasks`, `GET /api/tasks/byType/{taskType}`,
`GET /api/tasks/user/{userId}`, and `GET /api/users/{id}/tasks`) return
`PagedResult<TaskSummaryDto>`. Summary DTOs intentionally omit `customFields`;
fetch the task detail endpoint before editing or displaying custom payload data.
Assigned-task lists include closed tasks; use `currentStatus != 99` client-side
when a view needs only open work.

For full examples and status-specific payloads, see
[WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md). For API error codes and
troubleshooting, see [API_ERROR_CODES.md](API_ERROR_CODES.md).

## Task type validation

Validation is resolved by ordered `ITaskWorkflowRuleProvider`s:

1. `MetadataTaskWorkflowRuleProvider` (`Priority = 0`) validates active
   task-type metadata from the database.
2. `HandlerTaskWorkflowRuleProvider` (`Priority = 100`) falls back to
   registerable code handlers.

`HybridSchemaBootstrapper` seeds metadata for:

- `Procurement`, final status `3`
  - status `2`: `prices`, array of exactly two strings
  - status `3`: `receipt`, required string
- `Development`, final status `4`
  - status `2`: `specification`, string with minimum length `10`
  - status `3`: `branchName`, valid git branch string
  - status `4`: `versionNumber`, string or number matching semantic version rules

`AnalysisTaskHandler` and `TestingTaskHandler` are examples of registerable
code-backed task types; metadata-backed types do not need to implement
`IRegisterableTaskHandler`.

## Tests

The backend project includes xUnit tests in `backend/Tests` and has test
packages referenced directly in `DanTaskManager.csproj`.

```bash
dotnet test backend/DanTaskManager.csproj
```

The tests cover command/query delegation, task creation validation, task reads,
status movement rules, close constraints, metadata-vs-handler validation, and
closed-task immutability.

## Common pitfalls

- Use `customFields`, not `customDataJson`, in HTTP requests.
- Include `nextAssignedToUserId` on both status changes and close requests.
- Do not call `change-status` with `newStatus: 99`; close is only allowed
  through `POST /api/tasks/{id}/close`.
- Move a task to its final status before closing it.
- List responses are paged and do not include custom field payloads.

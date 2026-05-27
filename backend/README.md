# Dan Task Manager backend

.NET 8 API for task workflows backed by EF Core and SQL Server.

## Current workflow scope

The backend currently supports two task types:

- `Procurement`
- `Development`

The allow-list is defined in `Domain/WorkflowConstants.cs`. Unsupported task
types are rejected by task creation, workflow rule resolution, and task-type
metadata writes. Seed data also only creates Procurement and Development
metadata/tasks.

## Key directories

```text
backend/
├── Controllers/
│   ├── TasksController.cs       # Task CRUD and workflow endpoints
│   └── TaskTypesController.cs   # Task type metadata schema endpoints
├── Data/
│   └── ApplicationDbContext.cs  # EF model, seeded users/task metadata/tasks
├── Domain/
│   ├── BaseTask.cs
│   ├── WorkflowConstants.cs
│   └── Handlers/                # Procurement/Development fallback handlers
├── Services/
│   ├── TaskApplicationService.cs
│   ├── TaskWorkflowService.cs
│   ├── TaskWorkflowRuleProviders.cs
│   └── TaskTypeValidationService.cs
└── Validation/
    └── TaskRequestValidators.cs
```

## Setup

1. Restore packages:

   ```bash
   dotnet restore
   ```

2. Configure SQL Server in `appsettings.json`:

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=.;Database=DanTaskManager;Trusted_Connection=true;Encrypt=false;TrustServerCertificate=true;"
     }
   }
   ```

3. Run migrations or allow startup to apply them:

   ```bash
   dotnet ef database update
   dotnet run
   ```

`Program.cs` runs migrations when migrations exist, otherwise it calls
`EnsureCreated()`, then `HybridSchemaBootstrapper.EnsureSchema(dbContext)` to
ensure the hybrid metadata schema is present.

## Seed data

`ApplicationDbContext` seeds:

- 6 users with IDs `1` through `6`.
- Task type metadata for `Procurement` (final status `3`) and `Development`
  (final status `4`).
- Field rules:
  - Procurement status `2`: `prices` array with exactly two string values.
  - Procurement status `3`: `receipt` string.
  - Development status `2`: `specification` string, minimum 10 characters.
  - Development status `3`: `branchName` string matching `valid_git_branch`.
  - Development status `4`: `versionNumber` string/number matching
    `semantic_version`.
- One created Procurement task assigned to user `1`.
- One created Development task assigned to user `2`.

## API contract highlights

- `POST /api/tasks` creates a task at status `1`.
- `POST /api/tasks/{id}/change-status` requires:
  - `newStatus`
  - `nextAssignedToUserId`
  - `customFields` as a JSON object
- Status movement:
  - Forward movement must be exactly `+1`.
  - Backward movement can go to any lower status down to `1`.
  - Status `99` is closed and is only reached through
    `POST /api/tasks/{id}/close`.
  - Closing is allowed only from the task type final status.
- List endpoints return `PagedResult<TaskSummaryDto>` and omit `customFields`.
  Use `GET /api/tasks/{id}` for `TaskDetailsDto` with `customFields`.

See [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md) for full workflow
examples and [API_ERROR_CODES.md](API_ERROR_CODES.md) for request and error
contracts.

## Common pitfalls

- Use request property `customFields`; older references to `customDataJson` or
  `newDataJson` are stale API shapes.
- Do not start workflows at status `0`; `1` is the created status.
- Changing status replaces the stored custom data with the supplied
  `customFields` object.
- Adding a new task type requires coordinated backend and frontend changes:
  update `WorkflowConstants.SupportedTaskTypes`, DI handler registration, seeded
  metadata, validation/tests, and frontend task type/status mappings.

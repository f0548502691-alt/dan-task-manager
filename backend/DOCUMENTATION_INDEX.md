# Documentation index

Use this index to find the current engineering references for Dan Task Manager.
Generated example `.cs` files and the stale frontend `GENERAL_INSTRUCTIONS.md`
were removed; keep future docs close to the subsystem they describe.

## Current source-of-truth docs

| Document | Use it for |
|----------|------------|
| [README.md](README.md) | Backend setup, architecture, and API overview |
| [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md) | Task workflow rules, endpoint payloads, DTO boundaries |
| [API_ERROR_CODES.md](API_ERROR_CODES.md) | Error response shape, error codes, troubleshooting |
| [STRATEGY_PATTERN_DOCS.md](STRATEGY_PATTERN_DOCS.md) | Handler/provider extension architecture |
| [EXTENSION_GUIDE.md](EXTENSION_GUIDE.md) | Extension ideas and testing patterns; verify examples against source before copying |
| [BEST_PRACTICES.md](BEST_PRACTICES.md) | General backend coding conventions |
| [../frontend/README.md](../frontend/README.md) | Angular workflow board setup and API integration |

Older report/checklist files remain historical. Prefer the docs above for
current API names (`customFields`), statuses (`1` created, `99` closed), and
metadata-driven workflow behavior.

## By task

### Run the system locally

1. Start SQL Server and backend with Docker Compose from the repository root:
   `docker compose up --build`.
2. The backend listens on `http://localhost:8080`.
3. Start the Angular client from `frontend/` with `npm start`; `/api` is proxied
   to the backend.

See [README.md](README.md) and [../frontend/README.md](../frontend/README.md).

### Call the task API

Read [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md) for:

- task creation payloads
- status movement rules
- close-task constraints
- paged list response shape
- task detail response shape

### Debug API failures

Read [API_ERROR_CODES.md](API_ERROR_CODES.md). Backend errors normally use:

```json
{
  "error": "Description of what failed",
  "code": "workflow_validation_failed"
}
```

### Work on frontend workflow behavior

Read [../frontend/README.md](../frontend/README.md) before editing:

- `TaskService` signal ownership and response normalization
- `TaskWorkflowBoardComponent` form ownership
- type-specific field adapters
- global error handling
- zoneless Angular constraints

### Add or change a task type

Start with [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md) for runtime
constraints, then check [STRATEGY_PATTERN_DOCS.md](STRATEGY_PATTERN_DOCS.md) for
provider/handler architecture.

Current constraints:

- `WorkflowConstants.SupportedTaskTypes` gates task type support.
- Database metadata can override final status and field rules for supported
  types.
- Provider priority order is metadata first, handler fallback second.
- Frontend labels/adapters need updates for first-class UI fields; unknown types
  use the fallback JSON editor.

## Current project structure

```text
backend/
  Controllers/
    TasksController.cs
    TaskTypesController.cs
    UsersController.cs
  Contracts/Requests/
    Common/PaginationQuery.cs
    Tasks/*.cs
    TaskTypes/*.cs
  Data/
    ApplicationDbContext.cs
    HybridSchemaBootstrapper.cs
  Domain/
    WorkflowConstants.cs
    BaseTask.cs
    AppUser.cs
    Handlers/
  Services/
    TaskApplicationService.cs
    UserApplicationService.cs
    TaskWorkflowService.cs
    TaskWorkflowRuleProviders.cs
    TaskTypeValidationService.cs
    TaskDtoMappings.cs
  Tests/

frontend/
  src/app/core/
  src/app/tasks/
  proxy.conf.json
```

## Public API quick reference

### Tasks

```text
GET    /api/tasks
GET    /api/tasks/{id}
GET    /api/tasks/byType/{taskType}
GET    /api/tasks/user/{userId}
POST   /api/tasks
POST   /api/tasks/{id}/change-status
POST   /api/tasks/{id}/close
PUT    /api/tasks/{id}
DELETE /api/tasks/{id}
```

### Users

```text
GET /api/users
GET /api/users/{id}
GET /api/users/{id}/tasks
```

### Task type metadata

```text
GET  /api/task-types
GET  /api/task-types/{taskType}
GET  /api/task-types/{taskType}/schema
POST /api/task-types
POST /api/task-types/{taskType}/fields
PUT  /api/task-types/{taskType}/fields/{field}
```

## Documentation maintenance rules

- Verify public payload names against `Contracts/Requests` and controllers.
- Prefer concise updates to these canonical docs over adding new generated
  example files.
- Keep frontend docs under `frontend/` and backend docs under `backend/`.
- When documenting list responses, remember summaries omit `customFields`; detail
  responses include them.

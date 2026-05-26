# DanTaskManager Backend

.NET 8 API for task management with EF Core, SQL Server, workflow validation, and
task-type-specific handlers.

## Project shape

```
backend/
├── Controllers/
│   ├── TasksController.cs        # REST endpoints for task queries and workflow actions
│   ├── UsersController.cs        # User endpoints and user task queries
│   └── PaginationQuery.cs        # page/pageSize query binding
├── Data/ApplicationDbContext.cs  # EF Core model configuration and seed data
├── Domain/
│   ├── BaseTask.cs               # Task entity with CustomDataJson payload storage
│   ├── AppUser.cs                # User entity
│   ├── WorkflowConstants.cs      # CreatedStatus=1, ClosedStatus=99
│   └── Handlers/                 # Strategy handlers for task-specific validation
├── Services/
│   ├── TaskApplicationService.cs # Application facade for task CRUD/query/workflow calls
│   ├── TaskWorkflowService.cs    # Status movement, closure, and mutability rules
│   ├── UserApplicationService.cs # User query/create operations
│   └── QueryModels.cs            # DTOs and paged result contracts
├── Validation/                   # FluentValidation validators for request DTOs
└── Middleware/                   # Global API error responses
```

## Runtime architecture

Controllers stay thin:

1. Bind request/query DTOs.
2. Run FluentValidation where required.
3. Call an application service.
4. Return DTOs or let `WorkflowValidationException` flow to the global middleware.

Task workflow mutations go through:

```
TasksController
    -> ITaskApplicationService
        -> ITaskWorkflowService
            -> TaskHandlerFactory
                -> ITaskHandler for the task type
```

`TaskApplicationService` owns task queries, creation, description updates, and
delete operations. `TaskWorkflowService` owns status movement, closing, and
immutability checks. The removed status service should not be reintroduced for
new workflow work.

## Status and workflow constraints

- New tasks start at `WorkflowConstants.CreatedStatus` (`1`).
- Closed tasks use `WorkflowConstants.ClosedStatus` (`99`) and are immutable.
- Forward movement must be exactly `+1`.
- Rollback can move to any lower status greater than or equal to `1`.
- The same status is rejected.
- Status `99` is only reached through `POST /api/tasks/{id}/close`.
- Each handler defines its own `FinalStatus`; tasks cannot move forward beyond it.
- Every status change must include a valid `nextAssignedToUserId` and valid JSON in
  `newDataJson`.

Built-in handler payload requirements:

| Task type | Final status | Required payloads |
|-----------|--------------|-------------------|
| `Procurement` | `3` | status `2`: `{"prices":["5000","4800"]}`; status `3`: `{"receipt":"REC-001"}` |
| `Development` | `4` | status `2`: `{"specification":"..."}`; status `3`: `{"branchName":"feature/task"}`; status `4`: `{"versionNumber":"1.0.0"}` |

## API contracts

List endpoints return `PagedResult<T>`:

```json
{
  "items": [
    {
      "id": 12,
      "taskType": "Development",
      "currentStatus": 2,
      "assignedToUserId": 1,
      "description": "Build workflow screen",
      "createdAt": "2026-05-26T12:00:00Z",
      "updatedAt": "2026-05-26T12:10:00Z",
      "assignedToUser": { "id": 1, "name": "דן כהן", "email": "dan@example.com" }
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

`TaskSummaryDto` list items do not include `customDataJson`; fetch
`GET /api/tasks/{id}` for `TaskDetailsDto` when the custom payload is needed.

Common task endpoints:

| Method | Path | Notes |
|--------|------|-------|
| `GET` | `/api/tasks?page=1&pageSize=20` | All tasks, newest first |
| `GET` | `/api/tasks/{id}` | Detailed task including `customDataJson` |
| `GET` | `/api/tasks/byType/{taskType}` | Paged tasks filtered by type |
| `GET` | `/api/tasks/user/{userId}` | Paged tasks assigned to a user; 404 if user does not exist |
| `POST` | `/api/tasks` | Creates a task at status `1` |
| `POST` | `/api/tasks/{id}/change-status` | Applies workflow movement and reassigns the task |
| `POST` | `/api/tasks/{id}/close` | Closes a task from its handler final status |
| `PUT` | `/api/tasks/{id}` | Updates description unless task is closed |
| `DELETE` | `/api/tasks/{id}` | Deletes unless task is closed |

Create example:

```http
POST /api/tasks
Content-Type: application/json

{
  "taskType": "Procurement",
  "description": "רכישת ציוד",
  "assignedToUserId": 1,
  "customDataJson": "{}"
}
```

Change status example:

```http
POST /api/tasks/12/change-status
Content-Type: application/json

{
  "newStatus": 2,
  "nextAssignedToUserId": 2,
  "newDataJson": "{\"prices\":[\"5000\",\"4800\"]}"
}
```

Close example:

```http
POST /api/tasks/12/close
Content-Type: application/json

{
  "finalNotes": "הושלם ונמסר"
}
```

## Validation and errors

Request DTO validation is handled by FluentValidation classes under
`Validation/`. Business workflow failures throw `WorkflowValidationException`
from controllers and are converted by `GlobalExceptionMiddleware` to:

```json
{
  "error": "תנועה קדימה חייבת להיות בדיוק ב-1 סטטוס. סטטוס נוכחי: 1, מבוקש: 3",
  "code": "workflow_validation_failed"
}
```

Controller-level validation failures return 400 with an `error` message and no
`code` field. See `API_ERROR_CODES.md` for examples.

## Setup

1. Restore packages:

   ```bash
   dotnet restore
   ```

2. Configure `ConnectionStrings:DefaultConnection` in `appsettings.json`:

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=.;Database=DanTaskManager;Trusted_Connection=true;Encrypt=false;"
     }
   }
   ```

3. Apply migrations or run the app. `Program.cs` calls `Database.Migrate()` on
   startup, so a reachable SQL Server connection is required.

4. Run tests:

   ```bash
   dotnet test
   ```

## Common pitfalls

- Do not send status `0`; validators and workflow rules require positive statuses
  and created tasks start at status `1`.
- `newDataJson` is a JSON string in the request body. Escape it correctly when
  sending raw HTTP JSON.
- Include `nextAssignedToUserId` on every status change. The workflow service
  verifies the user exists before mutating the task.
- Add new task types as `ITaskHandler` implementations in
  `DanTaskManager.Domain.Handlers`; `Program.cs` registers handlers from that
  assembly automatically.
- Keep list/detail response expectations separate: list responses are paged
  summaries, while detail responses include `customDataJson`.

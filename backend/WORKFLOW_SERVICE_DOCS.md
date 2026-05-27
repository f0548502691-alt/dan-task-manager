# Task workflow service and API reference

This document describes the current workflow API and service boundaries verified
against `Controllers/TasksController.cs`, `Services/TaskWorkflowService.cs`,
`Services/TaskApplicationService.cs`, and the task request contracts.

## Architecture

```text
HTTP controllers
  -> FluentValidation request validators
  -> MediatR commands/queries
  -> TaskApplicationService / UserApplicationService
  -> TaskWorkflowService for status changes and close operations
  -> EF Core ApplicationDbContext
```

Workflow validation is split into two layers:

- `TaskWorkflowService` owns generic invariants: task exists, task is not closed,
  next assignee exists, `customFields` is a JSON object, status movement is legal,
  and closing is only allowed from the task type final status.
- `ITaskWorkflowRuleProvider` implementations own task-type rules. Providers are
  evaluated by ascending `Priority`; database metadata (`Priority = 0`) wins over
  handler fallback rules (`Priority = 100`).

The only built-in supported task types are listed in `WorkflowConstants`:

| Task type | Created | Final | Closed | Required fields by status |
|-----------|---------|-------|--------|---------------------------|
| Procurement | 1 | 3 | 99 | status 2: `prices` array of exactly two strings; status 3: `receipt` string |
| Development | 1 | 4 | 99 | status 2: `specification` string with at least 10 chars; status 3: `branchName`; status 4: `versionNumber` |

## Status movement rules

- New tasks start at status `1`.
- Forward movement must be exactly `+1`.
- Backward movement may target any lower status down to `1`.
- Requesting the current status fails.
- Status `99` cannot be set through `change-status`; use the close endpoint.
- Closed tasks are immutable for status changes, updates, and deletes.
- A task can only be closed from its final status.

## Public task endpoints

All list endpoints return `PagedResult<TaskSummaryDto>`:

```json
{
  "items": [
    {
      "id": 10,
      "taskType": "Procurement",
      "currentStatus": 2,
      "assignedToUserId": 1,
      "description": "Buy monitors",
      "createdAt": "2026-05-27T10:00:00Z",
      "updatedAt": "2026-05-27T10:05:00Z",
      "assignedToUser": {
        "id": 1,
        "name": "Dan Cohen",
        "email": "dan@example.com"
      }
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

`TaskSummaryDto` intentionally omits `customFields`. Use `GET /api/tasks/{id}`
when the UI or API consumer needs the full custom field payload.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/tasks?page=1&pageSize=20` | Paged task summaries, newest first |
| `GET` | `/api/tasks/{id}` | Full task details including `customFields` |
| `GET` | `/api/tasks/byType/{taskType}` | Paged summaries filtered by task type |
| `GET` | `/api/tasks/user/{userId}` | Paged summaries assigned to a user; 404 if user is missing |
| `POST` | `/api/tasks` | Create a task |
| `POST` | `/api/tasks/{id}/change-status` | Move status and optionally reassign |
| `POST` | `/api/tasks/{id}/close` | Close a task from final status |
| `PUT` | `/api/tasks/{id}` | Update description for non-closed tasks |
| `DELETE` | `/api/tasks/{id}` | Delete non-closed tasks |

Pagination comes from `PaginationQuery` and is normalized by `PageRequest`:
`page < 1` becomes `1`, `pageSize < 1` becomes `20`, and `pageSize` is capped at
`100`.

## Create task

```http
POST /api/tasks
Content-Type: application/json

{
  "taskType": "Procurement",
  "description": "Buy monitors",
  "assignedToUserId": 1,
  "customFields": {
    "priority": "medium"
  }
}
```

Notes:

- `taskType`, `description`, and `assignedToUserId` are required.
- `customFields` is optional on create, but when present it must be a JSON object.
- Unsupported task types fail with `task_type_validation_failed` and include the
  supported task type list in the message.

Response body is `TaskDetailsDto`:

```json
{
  "id": 10,
  "taskType": "Procurement",
  "currentStatus": 1,
  "assignedToUserId": 1,
  "description": "Buy monitors",
  "createdAt": "2026-05-27T10:00:00Z",
  "updatedAt": "2026-05-27T10:00:00Z",
  "customFields": {
    "priority": "medium"
  },
  "assignedToUser": {
    "id": 1,
    "name": "Dan Cohen",
    "email": "dan@example.com"
  }
}
```

## Change task status

```http
POST /api/tasks/10/change-status
Content-Type: application/json

{
  "newStatus": 2,
  "nextAssignedToUserId": 2,
  "customFields": {
    "prices": ["5000", "4800"]
  }
}
```

`customFields` is required for status changes and must be a JSON object. The
server stores it in `BaseTask.CustomDataJson`; clients should use the public
`customFields` request and response name.

Response:

```json
{
  "success": true,
  "message": "Status updated successfully to 2",
  "newStatus": 2,
  "task": {
    "id": 10,
    "taskType": "Procurement",
    "currentStatus": 2,
    "assignedToUserId": 2,
    "description": "Buy monitors",
    "createdAt": "2026-05-27T10:00:00Z",
    "updatedAt": "2026-05-27T10:05:00Z",
    "customFields": {
      "prices": ["5000", "4800"]
    },
    "assignedToUser": null
  }
}
```

## Close task

```http
POST /api/tasks/10/close
Content-Type: application/json

{
  "nextAssignedToUserId": 2,
  "finalNotes": "Purchase completed"
}
```

Closing validates that the task is currently at the final status for its type.
The workflow service appends `finalNotes` and `closedAt` into the stored custom
field JSON, then sets `currentStatus` to `99`.

Response:

```json
{
  "success": true,
  "message": "Task closed successfully",
  "task": {
    "id": 10,
    "taskType": "Procurement",
    "currentStatus": 99,
    "assignedToUserId": 2,
    "description": "Buy monitors",
    "createdAt": "2026-05-27T10:00:00Z",
    "updatedAt": "2026-05-27T10:15:00Z",
    "customFields": {
      "prices": ["5000", "4800"],
      "receipt": "REC-123",
      "finalNotes": "Purchase completed",
      "closedAt": "2026-05-27T10:15:00.0000000Z"
    },
    "assignedToUser": null
  }
}
```

## User and task type support endpoints

`UsersController` exposes read-only user data:

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/users?page=1&pageSize=20` | Paged users with task counts |
| `GET` | `/api/users/{id}` | Single user details |
| `GET` | `/api/users/{id}/tasks?page=1&pageSize=20` | Paged task summaries for a user |

`TaskTypesController` exposes metadata-backed task type schemas:

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/task-types` | Active and inactive task type schemas |
| `GET` | `/api/task-types/{taskType}` | One task type schema |
| `GET` | `/api/task-types/{taskType}/schema` | Alias for the same schema |
| `POST` | `/api/task-types` | Upsert task type metadata |
| `POST` | `/api/task-types/{taskType}/fields` | Upsert a field rule |
| `PUT` | `/api/task-types/{taskType}/fields/{field}` | Upsert a field rule using the path field name |

Metadata updates are still constrained by `WorkflowConstants.SupportedTaskTypes`;
adding a new task type requires code changes as well as metadata.

## DTO mapping constraints

`TaskDtoMappings.ToTaskSummary()` is an EF projection used by both
`TaskApplicationService` and `UserApplicationService`. Keep it expression-based
so EF Core can translate list queries without materializing full task rows.

Use these DTO boundaries:

- `TaskSummaryDto` for list endpoints; no `customFields`.
- `TaskDetailsDto` for detail, create, change-status, and close responses.
- `UserBriefDto` may be embedded in task summaries/details when the query joins
  the assigned user.

## Common pitfalls

- Do not send `customDataJson` or `newDataJson` in public API requests. Those are
  internal storage/legacy names; current controllers accept `customFields`.
- Do not assume status `0` exists. Created tasks start at `1`.
- Do not use `change-status` to close a task. Closing is a separate endpoint
  because it appends final notes and requires the final task status.
- Do not add list-page custom fields without checking payload size and frontend
  behavior; the UI loads details on selection to hydrate forms.

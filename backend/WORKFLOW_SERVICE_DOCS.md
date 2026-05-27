# Task workflow API and service guide

This guide documents the current task workflow behavior verified against
`TasksController`, `TaskApplicationService`, `TaskWorkflowService`,
`TaskWorkflowRuleProviders`, and the request validators.

## Source map

| Concern | Main files |
| --- | --- |
| HTTP routes and response wrapping | `Controllers/TasksController.cs`, `Controllers/UsersController.cs` |
| Request validation | `Validation/TaskRequestValidators.cs` |
| MediatR commands and queries | `Application/Tasks/*` |
| Task query/create/update/delete facade | `Services/TaskApplicationService.cs` |
| Status movement and close rules | `Services/TaskWorkflowService.cs` |
| Metadata/handler validation chain | `Services/TaskWorkflowRuleProviders.cs`, `Services/TaskTypeValidationService.cs` |
| DTO shapes and pagination | `Services/QueryModels.cs`, `Services/TaskProjectionExpressions.cs` |

## Core invariants

- New tasks start at status `1`.
- Closed tasks use status `99` and are immutable.
- Public API payloads use `customFields`; `CustomDataJson` is internal storage.
- `customFields` must be a JSON object whenever it is supplied. It is optional
  on create and required on status change.
- Status changes and closes both require a valid `nextAssignedToUserId`.
- `change-status` cannot set status `99`; use `close`.
- A task can only be closed from the final status returned by the selected
  workflow rule provider.
- List endpoints return paged summaries and omit `customFields`; detail/create/
  change/close reads return task details with `customFields`.

## Workflow rule resolution

`TaskWorkflowService` receives all `ITaskWorkflowRuleProvider`s and sorts them by
ascending `Priority`.

1. `MetadataTaskWorkflowRuleProvider` (`Priority = 0`) handles task types present
   in active metadata and validates DB-backed field rules.
2. `HandlerTaskWorkflowRuleProvider` (`Priority = 100`) handles task types
   registered by `IRegisterableTaskHandler`.

This means metadata wins when a task type exists in both places. The bootstrapper
seeds metadata for `Procurement` and `Development`, so those task types are
metadata-backed at runtime.

## Status movement rules

| Movement | Result |
| --- | --- |
| `currentStatus -> currentStatus + 1` | Allowed, then task-type payload rules run. |
| `currentStatus -> any lower status >= 1` | Allowed rollback, then payload rules for the target status run. |
| `currentStatus -> currentStatus` | Rejected. |
| `currentStatus -> currentStatus + 2` or higher | Rejected. |
| Any status -> `99` through `change-status` | Rejected; use close. |
| Forward movement after final status | Rejected. |
| Any movement when current status is `99` | Rejected. |

On successful change, `TaskWorkflowService` updates `CurrentStatus`,
`AssignedToUserId`, `CustomDataJson`, and `UpdatedAt`.

## Task type payload rules

The seeded metadata rules are:

### Procurement

Final status: `3`

| Target status | Required `customFields` |
| --- | --- |
| `2` | `{ "prices": ["5000", "4800"] }` - array of exactly two strings |
| `3` | `{ "receipt": "REC-001" }` - non-empty string |

### Development

Final status: `4`

| Target status | Required `customFields` |
| --- | --- |
| `2` | `{ "specification": "At least 10 chars" }` |
| `3` | `{ "branchName": "feature/task-123" }` - no spaces, no `//`, not ending in `/` or `.` |
| `4` | `{ "versionNumber": "1.2.0" }` - string or number matching semantic version rules |

The service persists the new payload as the full current `customFields` object.
If data from previous statuses is still needed, include it again in the status
change request.

## Required operation examples

### 1. Create task

```http
POST /api/tasks
Content-Type: application/json

{
  "taskType": "Procurement",
  "description": "Buy server components",
  "assignedToUserId": 1,
  "customFields": {}
}
```

Response: `201 Created` with `TaskDetailsDto`.

```json
{
  "id": 42,
  "taskType": "Procurement",
  "currentStatus": 1,
  "assignedToUserId": 1,
  "description": "Buy server components",
  "createdAt": "2026-05-27T12:00:00Z",
  "updatedAt": "2026-05-27T12:00:00Z",
  "assignedToUser": {
    "id": 1,
    "name": "Dan Cohen",
    "email": "dan@example.com"
  },
  "customFields": {}
}
```

Create validates that the assignee exists and that the task type is supported by
the task type catalog. Unsupported task type responses include the currently
supported task type names in the error message.

### 2. Get task

```http
GET /api/tasks/42
```

Response: `200 OK` with `TaskDetailsDto`, including `customFields`. A missing
task throws `ApiNotFoundException` and returns `{ "code": "not_found" }`.

### 3. Change status and reassign

```http
POST /api/tasks/42/change-status
Content-Type: application/json

{
  "newStatus": 2,
  "nextAssignedToUserId": 2,
  "customFields": {
    "prices": ["5000", "4800"]
  }
}
```

Response: `200 OK`.

```json
{
  "success": true,
  "message": "Status updated successfully to 2",
  "newStatus": 2,
  "task": {
    "id": 42,
    "taskType": "Procurement",
    "currentStatus": 2,
    "assignedToUserId": 2,
    "description": "Buy server components",
    "customFields": {
      "prices": ["5000", "4800"]
    }
  }
}
```

The controller reloads the task detail after the workflow service writes the
change, so callers receive the latest detail DTO.

### 4. Close task

Before closing, move the task to its final status (`3` for `Procurement`, `4`
for `Development`).

```http
POST /api/tasks/42/close
Content-Type: application/json

{
  "nextAssignedToUserId": 3,
  "finalNotes": "Delivered and verified"
}
```

Response: `200 OK`.

```json
{
  "success": true,
  "message": "Task closed successfully",
  "task": {
    "id": 42,
    "taskType": "Procurement",
    "currentStatus": 99,
    "assignedToUserId": 3,
    "customFields": {
      "receipt": "REC-001",
      "finalNotes": "Delivered and verified",
      "closedAt": "2026-05-27T12:10:00.0000000Z"
    }
  }
}
```

`WorkflowCloseData.Merge` preserves existing custom fields when the stored JSON
can be parsed as an object. If the stored JSON is invalid, close writes a new
object containing only `finalNotes` and `closedAt`.

## List and user endpoints

All list endpoints accept `page` and `pageSize` query parameters. `Page` values
less than `1` normalize to `1`. `PageSize` values less than `1` normalize to
`20`; values above `100` are capped at `100`.

```http
GET /api/tasks?page=1&pageSize=20
GET /api/tasks/byType/Development?page=1&pageSize=20
GET /api/tasks/user/1?page=1&pageSize=20
GET /api/users/1/tasks?page=1&pageSize=20
```

Response shape:

```json
{
  "items": [
    {
      "id": 42,
      "taskType": "Procurement",
      "currentStatus": 2,
      "assignedToUserId": 2,
      "description": "Buy server components",
      "createdAt": "2026-05-27T12:00:00Z",
      "updatedAt": "2026-05-27T12:05:00Z",
      "assignedToUser": {
        "id": 2,
        "name": "Ruth Levi",
        "email": "ruth@example.com"
      }
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

Assigned-task lists include both open and closed tasks. User summary DTOs expose
`openTasksCount` for dashboards that need an open-work count.

## Update and delete constraints

`PUT /api/tasks/{id}` can update the description of an open task.
`DELETE /api/tasks/{id}` can delete an open task. Both operations call
`EnsureTaskMutableAsync`; if the task is closed they return a workflow
validation error. If the task does not exist they return `not_found`.

## Operational checklist

When a workflow call fails:

1. Confirm the request uses `customFields` as an object.
2. Confirm `nextAssignedToUserId` points to an existing user.
3. Confirm the task is not closed (`currentStatus != 99`).
4. Confirm forward movement is exactly one status at a time.
5. Confirm the target status payload satisfies the task type schema.
6. For close, confirm the task is already at the task type final status.

# Task workflow service and API contract

This is the canonical workflow reference for the current backend. Verify changes
against `Controllers/TasksController.cs`, `Services/TaskWorkflowService.cs`,
`Services/TaskApplicationService.cs`, and `Domain/WorkflowConstants.cs`.

## Current scope

- Only `Procurement` and `Development` tasks are supported.
- The allow-list lives in `WorkflowConstants.SupportedTaskTypes`.
- Creating tasks, changing statuses, resolving workflow rule providers, and
  task-type metadata writes all reject task types outside that allow-list.
- `Program.cs` registers only `ProcurementTaskHandler` and
  `DevelopmentTaskHandler`; metadata validation is still preferred when active
  metadata exists for one of the supported types.

## Status model

| Status | Meaning |
| --- | --- |
| `1` | Created status for every new task (`WorkflowConstants.CreatedStatus`) |
| `2` | First type-specific data collection step |
| `3` | Procurement final status; Development branch step |
| `4` | Development final status |
| `99` | Closed status (`WorkflowConstants.ClosedStatus`) |

Rules enforced by `TaskWorkflowService`:

1. Closed tasks (`99`) cannot be changed.
2. Status `99` is only reached through `POST /api/tasks/{id}/close`.
3. Forward movement must be exactly `+1`.
4. Backward movement can go to any lower status, but not below `1`.
5. A task cannot move forward after it reaches its task type final status.
6. Closing is allowed only when the task is already at its final status.
7. Every status change must include a valid `nextAssignedToUserId`; the assignee
   is updated together with the status and `customFields`.

## Type-specific field requirements

Metadata from `TaskTypes` and `TaskFieldDefinitions` has priority through
`MetadataTaskWorkflowRuleProvider` (`Priority = 0`). If metadata is absent or
inactive, `HandlerTaskWorkflowRuleProvider` (`Priority = 100`) falls back to the
registered handler.

Seeded metadata currently defines:

| Task type | Final status | Status | Required `customFields` |
| --- | ---: | ---: | --- |
| `Procurement` | `3` | `2` | `{"prices":["5000","4800"]}` - array of exactly two strings |
| `Procurement` | `3` | `3` | `{"receipt":"REC-123"}` - non-empty string |
| `Development` | `4` | `2` | `{"specification":"at least 10 chars"}` |
| `Development` | `4` | `3` | `{"branchName":"feature/example"}` - valid git branch pattern |
| `Development` | `4` | `4` | `{"versionNumber":"1.2.0"}` - semantic version pattern |

Important payload constraint: `customFields` must be a JSON object. Arrays,
strings, numbers, and malformed JSON are rejected before workflow validation.

## REST API examples

### Create task

```http
POST /api/tasks
Content-Type: application/json

{
  "taskType": "Procurement",
  "description": "Collect hardware supplier quotes",
  "assignedToUserId": 1,
  "customFields": {}
}
```

Response: `201 Created` with `TaskDetailsDto`.

```json
{
  "id": 1,
  "taskType": "Procurement",
  "currentStatus": 1,
  "assignedToUserId": 1,
  "description": "Collect hardware supplier quotes",
  "createdAt": "2026-05-27T09:00:00Z",
  "updatedAt": "2026-05-27T09:00:00Z",
  "customFields": {},
  "assignedToUser": {
    "id": 1,
    "name": "דן כהן",
    "email": "dan@example.com"
  }
}
```

Unsupported task types return `400` and include the current allow-list:

```json
{
  "error": "סוג משימה לא נתמך: Analysis",
  "supportedTaskTypes": ["Development", "Procurement"]
}
```

### Change status and assignment

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

Response: `200 OK`.

```json
{
  "success": true,
  "message": "סטטוס עודכן בהצלחה ל-2",
  "newStatus": 2,
  "task": {
    "id": 1,
    "taskType": "Procurement",
    "currentStatus": 2,
    "assignedToUserId": 2,
    "description": "Collect hardware supplier quotes",
    "customFields": {
      "prices": ["5000", "4800"]
    },
    "assignedToUser": {
      "id": 2,
      "name": "רות לוי",
      "email": "ruth@example.com"
    }
  }
}
```

The request replaces the stored `CustomDataJson` with the supplied
`customFields` object. Send all data that should remain available after that
transition; do not rely on the service to merge old and new fields.

### Close task

```http
POST /api/tasks/1/close
Content-Type: application/json

{
  "finalNotes": "Completed and archived"
}
```

Close succeeds only from the task type final status. The service sets
`currentStatus` to `99` and writes `finalNotes` plus `closedAt` into the stored
custom data.

### List and detail reads

List endpoints return `PagedResult<TaskSummaryDto>` and do not include
`customFields`:

```http
GET /api/tasks?page=1&pageSize=20
GET /api/tasks/user/1?page=1&pageSize=20
GET /api/tasks/byType/Procurement?page=1&pageSize=20
```

```json
{
  "items": [
    {
      "id": 1,
      "taskType": "Procurement",
      "currentStatus": 2,
      "assignedToUserId": 1,
      "description": "Collect hardware supplier quotes",
      "createdAt": "2026-05-27T09:00:00Z",
      "updatedAt": "2026-05-27T09:05:00Z",
      "assignedToUser": null
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

Use `GET /api/tasks/{id}` when a client needs `customFields` for form
hydration.

## Task type metadata endpoints

```http
GET  /api/task-types
GET  /api/task-types/{taskType}
GET  /api/task-types/{taskType}/schema
POST /api/task-types
POST /api/task-types/{taskType}/fields
PUT  /api/task-types/{taskType}/fields/{field}
```

Reads return metadata for the seeded/configured task types. Writes are restricted
to `WorkflowConstants.SupportedTaskTypes`; posting metadata for `Analysis`,
`Testing`, or any other type returns `400` with `supportedTaskTypes`.

## Developer pitfalls

- Use `customFields`, not `customDataJson` or `newDataJson`, in API requests.
- Create starts at status `1`; examples that start at `0` are obsolete.
- Status change must include `nextAssignedToUserId`, even if assignment is not
  changing.
- List responses are paged summaries; call the detail endpoint before editing
  type-specific fields.
- Adding a new task type is no longer metadata-only. Update
  `WorkflowConstants.SupportedTaskTypes`, DI handler registration, seeded
  metadata, backend tests, and frontend type/status mappings together.

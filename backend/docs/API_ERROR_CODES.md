# API Error Codes

The backend returns a consistent JSON error shape from
`Middleware/GlobalExceptionMiddleware.cs`:

```json
{
  "error": "Human-readable message",
  "code": "machine_readable_code"
}
```

Clients should branch on `code` and show `error` as display text. Error messages
may change; codes are the stable contract.

## HTTP status mapping

| HTTP status | Source | Shape |
|-------------|--------|-------|
| `400 Bad Request` | `ApiValidationException`, `WorkflowValidationException` | `{ "error": "...", "code": "..." }` |
| `404 Not Found` | `ApiNotFoundException` | `{ "error": "...", "code": "not_found" }` |
| `500 Internal Server Error` | Unhandled exception | `{ "error": "An unexpected server error occurred", "code": "internal_server_error" }` |

## General API codes

| Code | Typical source | Meaning |
|------|----------------|---------|
| `validation_failed` | FluentValidation or manual request validation | The request body/query failed structural validation. |
| `task_type_validation_failed` | `POST /api/tasks`, `POST /api/task-types` | The task type is unsupported or invalid. |
| `task_creation_failed` | `POST /api/tasks` | The create workflow failed after request validation. |
| `task_type_field_validation_failed` | `POST /api/task-types/{taskType}/fields`, `PUT /api/task-types/{taskType}/fields/{field}` | A metadata field rule is invalid. |
| `not_found` | Detail reads for missing resources | The requested task/user resource does not exist. |
| `internal_server_error` | Unhandled exception | Unexpected server failure. |

## Workflow error codes

Workflow failures originate in `Services/WorkflowErrorCodes.cs` and are returned
as `400 Bad Request`.

| Code | When it occurs |
|------|----------------|
| `task_not_found` | The workflow command references a missing task. |
| `task_closed` | A status update tries to mutate a closed task. |
| `task_already_closed` | A close request targets a task that is already closed. |
| `assignee_not_found` | `nextAssignedToUserId` does not reference an existing user. |
| `invalid_custom_data_json` | The service receives invalid custom-field JSON. |
| `unsupported_task_type` | No metadata or code-backed handler supports the task type. |
| `illegal_status_transition` | Forward movement is not exactly `+1`, or backward movement is below created status. |
| `final_status_reached` | A task already at its final status is advanced with `change-status`. |
| `close_requires_final_status` | `close` is called before the task reaches its type-specific final status. |
| `close_via_close_task_only` | A request tries to move to status `99` through `change-status`. |
| `same_status` | The requested status equals the current status. |
| `field_validation_failed` | Task-type metadata or handler validation rejects `customFields`. |

## Current request contracts

Create a task:

```http
POST /api/tasks
Content-Type: application/json
```

```json
{
  "taskType": "Marketing",
  "description": "Launch spring campaign",
  "assignedToUserId": 1,
  "customFields": {}
}
```

Change status:

```http
POST /api/tasks/1/change-status
Content-Type: application/json
```

```json
{
  "newStatus": 2,
  "nextAssignedToUserId": 2,
  "customFields": {
    "campaignName": "Spring campaign",
    "targetAudience": "B2B"
  }
}
```

Close a task:

```http
POST /api/tasks/1/close
Content-Type: application/json
```

```json
{
  "nextAssignedToUserId": 2,
  "finalNotes": "Ready for archive"
}
```

The public API uses `customFields`. `CustomDataJson` is the internal EF column
on `BaseTask`, not a request property. Retired request names such as
`newDataJson` and `customDataJson` should not be used by clients.

## Response examples

Validation failure:

```json
{
  "error": "Description is required",
  "code": "validation_failed"
}
```

Unsupported task type:

```json
{
  "error": "Unsupported task type: Unknown. Supported task types: Analysis, Development, Marketing, Procurement, Testing",
  "code": "task_type_validation_failed"
}
```

Workflow failure:

```json
{
  "error": "Forward movement must be exactly +1. Current status: 1, requested status: 3",
  "code": "illegal_status_transition"
}
```

Successful status change:

```json
{
  "success": true,
  "message": "Status changed successfully to 2",
  "newStatus": 2,
  "task": {
    "id": 1,
    "taskType": "Marketing",
    "currentStatus": 2,
    "assignedToUserId": 2,
    "description": "Launch spring campaign",
    "createdAt": "2026-05-25T00:00:00Z",
    "updatedAt": "2026-05-25T00:05:00Z",
    "assignedToUser": {
      "id": 2,
      "name": "Ruth Levi",
      "email": "ruth@example.com"
    },
    "customFields": {
      "campaignName": "Spring campaign",
      "targetAudience": "B2B"
    }
  }
}
```

List endpoints return `PagedResult<T>`:

```json
{
  "items": [
    {
      "id": 1,
      "taskType": "Procurement",
      "currentStatus": 1,
      "assignedToUserId": 1,
      "description": "Collect supplier quotes for new equipment",
      "createdAt": "2026-05-25T00:00:00Z",
      "updatedAt": "2026-05-25T00:00:00Z",
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

List summaries do not include `customFields`; use `GET /api/tasks/{id}` for the
detail DTO when a client needs editable custom field data.

## Troubleshooting by code

| Code | First checks |
|------|--------------|
| `validation_failed` | Confirm required body fields and query values. `page` and `pageSize` default to `1` and `20`; `pageSize` is capped at `100`. |
| `task_type_validation_failed` | Call `GET /api/task-types` and use the returned `taskType` value exactly. |
| `field_validation_failed` | Compare `customFields` with `GET /api/task-types/{taskType}` for the target status. |
| `assignee_not_found` | Use one of the seeded users or verify the user through `GET /api/users/{id}`. |
| `close_requires_final_status` | Move through each status until the task type's `finalStatus`, then call `close`. |
| `internal_server_error` | Check backend logs; the middleware hides implementation details from clients. |

Related docs:

- `backend/docs/WORKFLOW.md` - workflow rules and status constants.
- `backend/docs/QUICKSTART.md` - runnable curl examples.
- `backend/docs/EXTENSION_GUIDE.md` - adding task types and metadata fields.

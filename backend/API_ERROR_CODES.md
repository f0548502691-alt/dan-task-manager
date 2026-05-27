# API error codes and troubleshooting

The API uses `GlobalExceptionMiddleware` for controller-thrown `ApiException`s.
Most task workflow failures are returned as `400 Bad Request` with a stable
machine-readable `code` and a human-readable `error`.

```json
{
  "error": "Task is closed - status cannot be changed",
  "code": "workflow_validation_failed"
}
```

## Error response contract

| HTTP status | Code | Raised by | Typical cause |
| --- | --- | --- | --- |
| `400` | `validation_failed` | `ApiValidationException` | Request model validation failed, such as missing `customFields` or invalid IDs. |
| `400` | `workflow_validation_failed` | `WorkflowValidationException` | Status movement, close, task mutability, unsupported workflow type, or assignee rule failed. |
| `400` | `task_type_validation_failed` | `ApiValidationException` | Create/upsert referenced an unsupported or invalid task type. |
| `400` | `task_creation_failed` | `ApiValidationException` | Create failed after request validation, such as missing user or invalid custom JSON. |
| `400` | `task_type_field_validation_failed` | `ApiValidationException` | Task type field metadata is invalid. |
| `404` | `not_found` | `ApiNotFoundException` | Task or user was not found. |
| `500` | `internal_server_error` | middleware fallback | Unhandled exception. |

Some `TaskTypesController` validation branches return `BadRequest(new { error =
... })` directly and may not include `code`. Task workflow endpoints use the
middleware-backed shape above.

## Request validation errors

### Create task

`POST /api/tasks` is validated by `CreateTaskRequestValidator`.

| Field | Constraint | Example error |
| --- | --- | --- |
| `taskType` | required | `TaskType is required` |
| `description` | required | `Description is required` |
| `assignedToUserId` | greater than `0` | `AssignedToUserId must be greater than 0` |
| `customFields` | optional, but must be an object when supplied | `CustomFields must be a valid JSON object` |

### Change status

`POST /api/tasks/{id}/change-status` is validated by
`ChangeStatusWorkflowRequestValidator`.

| Field | Constraint | Example error |
| --- | --- | --- |
| `newStatus` | greater than `0` | `NewStatus must be greater than 0` |
| `nextAssignedToUserId` | greater than `0` | `NextAssignedToUserId is required` |
| `customFields` | required JSON object | `CustomFields is required` or `CustomFields must be a valid JSON object` |

### Close task

`POST /api/tasks/{id}/close` is validated by `CloseTaskRequestValidator`.

| Field | Constraint | Example error |
| --- | --- | --- |
| `nextAssignedToUserId` | greater than `0` | `NextAssignedToUserId נדרש` |
| `finalNotes` | required | `FinalNotes is required` |

## Workflow validation errors

These messages come from `TaskWorkflowService` and the selected workflow rule
provider.

| Scenario | Example message | How to fix |
| --- | --- | --- |
| Task does not exist | `Task does not exist` | Use an existing task ID; public controller reads use `404 not_found`. |
| Task is closed | `Task is closed - status cannot be changed` | Closed tasks are immutable; create a new task if more work is needed. |
| Close already closed task | `Task is already closed` | Do not close the same task twice. |
| Unknown next assignee | `Next assignee does not exist` | Send a valid `nextAssignedToUserId`. |
| Missing/invalid JSON object | `customFields must be a valid JSON object` | Send `{}` or an object with the required fields. |
| Unsupported task type | `Unsupported task type: <type>` | Check `GET /api/task-types` for supported task types. |
| Close before final status | `A Procurement task can only be closed from final status 3` | Move one status at a time until the final status, then call close. |
| Closing through change-status | `Task closing is allowed only via CloseTask` | Call `POST /api/tasks/{id}/close` instead of sending `newStatus: 99`. |
| Status below created | `Status must be 1 or higher` | Do not move to status `0` or negative statuses. |
| Forward jump | `Forward movement must be exactly +1 status. Current status: 1, requested: 3` | Move to the intermediate status first. |
| Same status | `New status is identical to current status` | Send a different target status. |
| Beyond final status | `Task already reached final status (3)` | Close the task or roll it back; do not move forward. |

## Task type payload errors

Payload validation is metadata-first, then handler fallback.

### Procurement

| Target status | Bad payload | Example error |
| --- | --- | --- |
| `2` | missing `prices` | `Status 2 requires a 'prices' field containing an array of two quote strings` |
| `2` | `prices` is not an array | `'prices' must be an array` |
| `2` | wrong number of prices | `'prices' must contain exactly 2 strings, found 1` |
| `2` | price is empty or non-string | `All prices must be strings` or `Price values cannot be empty` |
| `3` | missing `receipt` | `Status 3 requires a 'receipt' field containing a receipt string` |
| `3` | receipt is not a non-empty string | `'receipt' must be a string` or `'receipt' cannot be empty` |

### Development

| Target status | Bad payload | Example error |
| --- | --- | --- |
| `2` | missing or short `specification` | `Status 2 requires a 'specification' field containing specification text` or `'specification' must be at least 10 characters long` |
| `3` | missing or invalid `branchName` | `Status 3 requires a 'branchName' field containing a branch name` or `Invalid branch name (cannot contain //, end with / or ., or include spaces)` |
| `4` | missing or invalid `versionNumber` | `Status 4 requires a 'versionNumber' field containing a version number`, `'versionNumber' must be a string or a number`, or `'versionNumber' must follow SemVer format (for example: 1.0.0), received: <value>` |

DB metadata validation may produce equivalent field-rule messages when metadata
is active for the task type.

## Success response reminders

### Create and get detail

Create (`201 Created`) and get detail (`200 OK`) return `TaskDetailsDto`:

```json
{
  "id": 42,
  "taskType": "Development",
  "currentStatus": 1,
  "assignedToUserId": 1,
  "description": "Build API",
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

### Change status

Change status returns a wrapper with `success`, `message`, `newStatus`, and a
freshly reloaded task detail.

```json
{
  "success": true,
  "message": "Status updated successfully to 2",
  "newStatus": 2,
  "task": {
    "id": 42,
    "currentStatus": 2,
    "assignedToUserId": 2,
    "customFields": {
      "specification": "Documented feature behavior"
    }
  }
}
```

### Close

Close returns a wrapper with `success`, `message`, and a reloaded task detail.
The detail has `currentStatus: 99`; `customFields` includes `finalNotes` and an
ISO-8601 `closedAt` timestamp.

## Debug checklist

1. Confirm the client sends `customFields`, not `customDataJson` or
   `newDataJson`.
2. For list responses, confirm the client reads `items` from `PagedResult<T>`.
3. For detail/form screens, fetch `GET /api/tasks/{id}` because summaries omit
   `customFields`.
4. For close failures, check the current task status against
   `GET /api/task-types/{taskType}` and its `finalStatus`.
5. For assignee failures, verify the user exists through `GET /api/users/{id}`.

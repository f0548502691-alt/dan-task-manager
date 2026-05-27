# API error codes and troubleshooting

The backend uses `GlobalExceptionMiddleware` to return consistent JSON errors
for exceptions derived from `ApiException` and for unhandled failures.

## Error shape

```json
{
  "error": "Description of what failed",
  "code": "validation_failed"
}
```

Clients should display `error` and may branch on `code` for targeted handling.

## Codes emitted by the API

| Code | HTTP status | Source | Typical cause |
|------|-------------|--------|---------------|
| `validation_failed` | 400 | `ApiValidationException` default | FluentValidation request failure |
| `workflow_validation_failed` | 400 | `WorkflowValidationException` | Invalid movement, closed task, unsupported workflow state |
| `task_type_validation_failed` | 400 | task type metadata and create flow | Unsupported task type or invalid metadata |
| `task_creation_failed` | 400 | create flow | User missing, invalid custom fields, post-create load failure |
| `task_type_field_validation_failed` | 400 | task type field metadata | Invalid field rule definition |
| `not_found` | 404 | `ApiNotFoundException` | Task, user, or task type does not exist |
| `internal_server_error` | 500 | global fallback | Unhandled exception |

Some controller branches still return `BadRequest(new { error, supportedTaskTypes })`
directly, for example unsupported task type field updates. Those responses do
not include a `code`.

## Request validation failures

### Create task

```http
POST /api/tasks
Content-Type: application/json

{
  "taskType": "",
  "description": "",
  "assignedToUserId": 0,
  "customFields": []
}
```

Response:

```json
{
  "error": "TaskType is required; Description is required; AssignedToUserId must be greater than 0; CustomFields must be a valid JSON object",
  "code": "validation_failed"
}
```

`customFields` may be omitted on create, but if present it must be a JSON object.

### Change status

```http
POST /api/tasks/10/change-status
Content-Type: application/json

{
  "newStatus": 2,
  "nextAssignedToUserId": 0,
  "customFields": null
}
```

Response:

```json
{
  "error": "NextAssignedToUserId is required; CustomFields is required",
  "code": "validation_failed"
}
```

`customFields` is required on status changes because task-type validators inspect
the full next payload.

### Close task

```json
{
  "nextAssignedToUserId": 2,
  "finalNotes": ""
}
```

Response:

```json
{
  "error": "FinalNotes is required",
  "code": "validation_failed"
}
```

## Workflow validation failures

The workflow service returns these as `workflow_validation_failed` through
`TasksController`.

| Scenario | Example response |
|----------|------------------|
| Forward jump skips a status | `{ "error": "Forward movement must be exactly +1 status. Current status: 1, requested: 3", "code": "workflow_validation_failed" }` |
| Same status requested | `{ "error": "New status is identical to current status", "code": "workflow_validation_failed" }` |
| Status below created | `{ "error": "Status must be 1 or higher", "code": "workflow_validation_failed" }` |
| Direct close through change-status | `{ "error": "Task closing is allowed only via CloseTask", "code": "workflow_validation_failed" }` |
| Closed task changed | `{ "error": "Task is closed - status cannot be changed", "code": "workflow_validation_failed" }` |
| Next assignee missing | `{ "error": "Next assignee does not exist", "code": "workflow_validation_failed" }` |
| Non-object custom fields | `{ "error": "customFields must be a valid JSON object", "code": "workflow_validation_failed" }` |
| Close before final status | `{ "error": "A Procurement task can only be closed from final status 3", "code": "workflow_validation_failed" }` |

## Task-type validation failures

Built-in handlers and metadata seed the same basic rules:

| Task type | Status | Required payload |
|-----------|--------|------------------|
| Procurement | 2 | `{ "prices": ["5000", "4800"] }` |
| Procurement | 3 | `{ "receipt": "REC-123" }` |
| Development | 2 | `{ "specification": "At least ten characters" }` |
| Development | 3 | `{ "branchName": "feature/task-10" }` |
| Development | 4 | `{ "versionNumber": "1.0.0" }` |

Example missing Procurement prices:

```json
{
  "error": "Field 'prices' is required for status 2",
  "code": "workflow_validation_failed"
}
```

When database metadata is present, exact messages come from
`TaskTypeValidationService`. If metadata is unavailable for a supported type, the
handler fallback messages come from `ProcurementTaskHandler` or
`DevelopmentTaskHandler`.

## Not found failures

Examples:

```json
{
  "error": "משימה לא נמצאה",
  "code": "not_found"
}
```

```json
{
  "error": "משתמש לא קיים",
  "code": "not_found"
}
```

The users and tasks controllers check existence before returning user-specific
task lists, update/delete failures, and detail responses.

## Successful response reminders

- Create returns `201 Created` with `TaskDetailsDto`.
- List endpoints return `PagedResult<TaskSummaryDto>` and omit `customFields`.
- Detail, create, change-status, and close responses include `customFields`.
- Public request/response payloads use `customFields`; `CustomDataJson` is the
  internal database property.

## Client troubleshooting checklist

1. Verify the request uses `customFields`, not `customDataJson` or `newDataJson`.
2. Verify status numbers start at `1` and closed is `99`.
3. Fetch `GET /api/task-types` if the UI needs the latest final status and field
   rules.
4. For list rows missing custom field data, fetch `GET /api/tasks/{id}` before
   hydrating type-specific forms.

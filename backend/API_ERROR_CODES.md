# API Errors and Troubleshooting

The API returns JSON error bodies for validation and workflow failures. Most
controller-level validation errors use `{ "error": "..." }`; unsupported task
type creation also includes `supportedTaskTypes`.

## HTTP status codes

| Status | Meaning | Typical source |
|--------|---------|----------------|
| `200 OK` | Request succeeded | Reads, status changes, close operations. |
| `201 Created` | Task created | `POST /api/tasks`. |
| `204 No Content` | Mutation succeeded without body | Update description, delete. |
| `400 Bad Request` | Validation or workflow failure | Invalid request, unsupported task type, invalid movement, invalid payload. |
| `404 Not Found` | Resource is missing | Missing task, user, or task type schema. |
| `500 Internal Server Error` | Unhandled server issue | Middleware returns the standard server error body. |

## Common errors

### Unsupported task type

```json
{
  "error": "Unsupported task type: Unknown",
  "supportedTaskTypes": ["Analysis", "Development", "Procurement", "Testing"]
}
```

Fix: use a task type returned by `GET /api/task-types`, add active metadata, or
add an `IRegisterableTaskHandler`.

### Invalid status movement

```json
{
  "error": "Forward movement must be exactly +1 status. Current status: 1, requested: 3"
}
```

Fix: move forward one status at a time. Backward movement to a lower status is
allowed.

### Same status

```json
{
  "error": "New status is identical to current status"
}
```

Fix: choose a different `newStatus`.

### Status below created status

```json
{
  "error": "Status must be 1 or higher"
}
```

Fix: status `0` is not used by this workflow; new tasks start at `1`.

### Attempt to set closed status through change-status

```json
{
  "error": "Task closing is allowed only via CloseTask"
}
```

Fix: call `POST /api/tasks/{id}/close` with `finalNotes` and
`nextAssignedToUserId`.

### Task is already closed

```json
{
  "error": "Task is closed - status cannot be changed"
}
```

Fix: closed status `99` is terminal. Create a new task if more work is needed.

### Final status reached

```json
{
  "error": "Task already reached final status (3)"
}
```

Fix: close the task or roll back to a lower status before continuing.

### Missing or invalid next assignee

```json
{
  "error": "Next assignee does not exist"
}
```

Fix: send a valid seeded or persisted user ID in `nextAssignedToUserId`.

### Invalid customFields payload

```json
{
  "error": "customFields must be a valid JSON object"
}
```

Fix: send `customFields` as an object, for example `{}`. Arrays, strings, and
malformed JSON are rejected.

### Metadata field validation

```json
{
  "error": "Status 2 requires field 'prices'"
}
```

```json
{
  "error": "Field 'prices' must contain exactly 2 items"
}
```

```json
{
  "error": "Field 'branchName' is not a valid branch name"
}
```

Fix: compare the target status and required fields with `GET /api/task-types`.
Field rules apply only when the requested status is inside the rule's status
range.

## Request field names

Use these public names:

- `customFields` on create and status change requests.
- `nextAssignedToUserId` on status change and close requests.
- `finalNotes` on close requests.

Do not send legacy/internal names such as `customDataJson` or `newDataJson` to
the public API.

## Debugging checklist

1. Fetch `GET /api/task-types` and verify the task type is listed and active.
2. Check the task detail with `GET /api/tasks/{id}` for current status and
   current assignee.
3. Confirm forward movement is exactly `+1` and the task is not closed.
4. Confirm `customFields` is a JSON object and contains fields required for the
   target status.
5. For close failures, confirm the task is exactly at its final status.

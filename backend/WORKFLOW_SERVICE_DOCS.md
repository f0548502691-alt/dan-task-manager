# Task Workflow Service

`TaskWorkflowService` owns the task state machine. Controllers and MediatR
handlers pass already-shaped requests to the application service; the workflow
service enforces the invariants that must stay true for every task type.

## Responsibilities

- Load and mutate `BaseTask` rows.
- Reject changes to closed tasks.
- Validate the next assignee exists.
- Require `customFields` to serialize to a JSON object before status changes.
- Resolve the first `ITaskWorkflowRuleProvider` that can handle the task type.
- Enforce movement and final-status rules.
- Delegate type-specific payload validation to the chosen provider.
- Merge `finalNotes` and `closedAt` into `CustomDataJson` when closing.

## Rule provider chain

Providers are injected as `IEnumerable<ITaskWorkflowRuleProvider>` and sorted by
ascending `Priority`.

| Provider | Priority | Handles | Source of final status and validation |
|----------|----------|---------|----------------------------------------|
| `MetadataTaskWorkflowRuleProvider` | `0` | Task types known to `ITaskTypeValidationService` | `TaskTypes` and `TaskFieldDefinitions` metadata. |
| `HandlerTaskWorkflowRuleProvider` | `100` | Task types registered in `TaskHandlerFactory` | `IRegisterableTaskHandler` implementations. |

Metadata-backed task types therefore override handlers when both exist.

## Status rules

| Rule | Behavior |
|------|----------|
| Created status | New tasks start at `1`. |
| Closed status | `99`; only `CloseTaskAsync` can set it. |
| Forward movement | Must be exactly `currentStatus + 1`. |
| Backward movement | Any lower status is allowed if it is at least `1`. |
| Same status | Rejected. |
| Final status | A task that has reached final status cannot advance further. |
| Closing | Allowed only when `CurrentStatus == finalStatus`. |

## Public API examples

### Create task

```http
POST /api/tasks
Content-Type: application/json

{
  "taskType": "Development",
  "description": "Build import workflow",
  "assignedToUserId": 1,
  "customFields": {}
}
```

Success returns `201 Created` with a `TaskDetailsDto`:

```json
{
  "id": 12,
  "taskType": "Development",
  "currentStatus": 1,
  "assignedToUserId": 1,
  "description": "Build import workflow",
  "customFields": {},
  "createdAt": "2026-05-27T11:00:00Z",
  "updatedAt": "2026-05-27T11:00:00Z"
}
```

Unknown task types return `400` and include the current catalog:

```json
{
  "error": "Unsupported task type: Unknown",
  "supportedTaskTypes": ["Analysis", "Development", "Procurement", "Testing"]
}
```

### Change status

```http
POST /api/tasks/12/change-status
Content-Type: application/json

{
  "newStatus": 2,
  "nextAssignedToUserId": 2,
  "customFields": {
    "specification": "Import CSV files into the task board"
  }
}
```

The response includes the updated task details:

```json
{
  "success": true,
  "message": "Status updated successfully to 2",
  "newStatus": 2,
  "task": {
    "id": 12,
    "taskType": "Development",
    "currentStatus": 2,
    "assignedToUserId": 2,
    "customFields": {
      "specification": "Import CSV files into the task board"
    }
  }
}
```

### Close task

```http
POST /api/tasks/12/close
Content-Type: application/json

{
  "nextAssignedToUserId": 2,
  "finalNotes": "Released in 1.0.0"
}
```

Closing sets status `99` and preserves prior custom fields while adding
`finalNotes` and `closedAt`.

### List and details

```http
GET /api/tasks?page=1&pageSize=20
GET /api/tasks/user/1?page=1&pageSize=20
GET /api/tasks/byType/Development?page=1&pageSize=20
GET /api/tasks/12
```

List endpoints return `PagedResult<TaskSummaryDto>` and omit `customFields` for
cheap projections. Details, create, status-change, and close responses return
`TaskDetailsDto` with parsed `customFields`.

## Metadata-backed validation

`TaskTypeValidationService` reads active task type metadata from the database and
caches definitions for five minutes. Field rules apply when the target status is
between `appliesFromStatus` and `appliesToStatus` (inclusive). Supported rule
properties include:

- `type`: `string`, `number`, `array`, `object`, `boolean`, or `stringOrNumber`.
- `required`
- `minLength` / `maxLength`
- `minValue` / `maxValue`
- `arrayLength`, `minItems`, `maxItems`, and `elementType`
- `allowedValues`
- `pattern`: `valid_git_branch`, `semantic_version`, or a regular expression

Validation runs on status changes, not on task creation.

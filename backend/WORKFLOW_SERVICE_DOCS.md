# Task Workflow Service

`TaskWorkflowService` is the backend authority for task status movement, closure,
and closed-task immutability. It verifies source data against persisted tasks and
delegates task-type-specific payload checks to `ITaskHandler` strategies.

## Architecture

```
TasksController
    validates request DTOs with FluentValidation
    -> ITaskApplicationService
        maps API operations to application commands/DTOs
        -> ITaskWorkflowService
            enforces workflow invariants
            -> TaskHandlerFactory
                selects ProcurementTaskHandler, DevelopmentTaskHandler, ...
```

`TaskWorkflowService` uses `ApplicationDbContext` directly for workflow updates.
It returns `WorkflowResult` to the application service; controllers turn failed
workflow results into `WorkflowValidationException`, which the global middleware
maps to HTTP 400.

## Public interface

```csharp
public interface ITaskWorkflowService
{
    Task<WorkflowResult> ChangeStatusAsync(
        int taskId,
        int newStatus,
        int nextAssignedToUserId,
        string newDataJson,
        CancellationToken cancellationToken = default);

    Task<WorkflowResult> CloseTaskAsync(
        int taskId,
        string finalNotes,
        CancellationToken cancellationToken = default);

    Task<WorkflowResult> EnsureTaskMutableAsync(
        int taskId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<BaseTask>> GetUserTasksAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<BaseTask?> GetTaskAsync(
        int taskId,
        CancellationToken cancellationToken = default);
}
```

Main callers:

- `TaskApplicationService.ChangeStatusAsync` -> `ChangeStatusAsync`
- `TaskApplicationService.CloseAsync` -> `CloseTaskAsync`
- `TaskApplicationService.UpdateDescriptionAsync` and `DeleteAsync` ->
  `EnsureTaskMutableAsync`

## Status constants

Use `WorkflowConstants` rather than magic numbers in new code:

| Constant | Value | Meaning |
|----------|-------|---------|
| `WorkflowConstants.CreatedStatus` | `1` | Initial status for newly created tasks |
| `WorkflowConstants.ClosedStatus` | `99` | Permanent closed status |

Status `99` is only valid through `CloseTaskAsync`. A normal status change to 99
is rejected with "סגירת משימה מתבצעת רק דרך CloseTask".

## ChangeStatusAsync flow

`ChangeStatusAsync(taskId, newStatus, nextAssignedToUserId, newDataJson)` performs
these checks in order:

1. Load the task. Missing tasks fail with "משימה לא קיימת".
2. Reject closed tasks (`CurrentStatus == 99`).
3. Verify `nextAssignedToUserId` exists in `Users`.
4. Verify `newDataJson` is non-empty valid JSON.
5. Resolve a handler for `task.TaskType`; unknown task types fail.
6. Validate movement rules:
   - `newStatus` must be `>= 1`.
   - forward movement must be exactly `currentStatus + 1`.
   - rollback can move to any lower status.
   - same-status movement is rejected.
   - once the task is at or beyond handler `FinalStatus`, forward movement is
     rejected.
7. Call `handler.ValidateStatusChange(currentDataJson, currentStatus, newStatus, newDataJson)`.
8. Persist:
   - `CurrentStatus = newStatus`
   - `AssignedToUserId = nextAssignedToUserId`
   - `CustomDataJson = newDataJson`
   - `UpdatedAt = DateTime.UtcNow`

### Valid movement examples

```
1 -> 2       allowed, forward exactly +1
2 -> 3       allowed if handler payload validation passes
3 -> 2       allowed rollback
3 -> 1       allowed rollback
```

### Invalid movement examples

```
1 -> 3       rejected, skipped status 2
2 -> 2       rejected, no state change
1 -> 0       rejected, statuses start at 1
3 -> 4       rejected for Procurement because FinalStatus is 3
3 -> 99      rejected by ChangeStatusAsync; use CloseTaskAsync
```

## CloseTaskAsync flow

`CloseTaskAsync(taskId, finalNotes)`:

1. Loads the task.
2. Rejects already closed tasks.
3. Resolves the handler for `TaskType`.
4. Requires `CurrentStatus == handler.FinalStatus`.
5. Adds `finalNotes` and ISO-8601 `closedAt` to `CustomDataJson`.
6. Sets `CurrentStatus = WorkflowConstants.ClosedStatus`.
7. Updates `UpdatedAt` and saves.

If the existing custom data cannot be parsed, closure replaces it with a JSON
object containing only `finalNotes` and `closedAt`.

## EnsureTaskMutableAsync flow

`EnsureTaskMutableAsync` is used before non-status mutations:

- description updates (`PUT /api/tasks/{id}`)
- deletes (`DELETE /api/tasks/{id}`)

It fails when the task is missing or closed. Controllers distinguish these cases
by reloading the task: missing tasks return 404, while closed tasks become a
workflow validation error.

## Handler payload rules

`ITaskHandler` implementations validate `newDataJson` for statuses that have
task-type-specific requirements. Statuses not registered in the handler's status
validation map pass handler validation after the global movement checks pass.

### Procurement

Final status: `3`

| Target status | Required JSON |
|---------------|---------------|
| `2` | `{"prices":["5000","4800"]}`; exactly two non-empty strings |
| `3` | `{"receipt":"REC-001"}`; non-empty string |

### Development

Final status: `4`

| Target status | Required JSON |
|---------------|---------------|
| `2` | `{"specification":"At least 10 chars"}` |
| `3` | `{"branchName":"feature/workflow-refactor"}`; no spaces, `//`, trailing `/`, or trailing `.` |
| `4` | `{"versionNumber":"1.0.0"}` or numeric version value |

## REST examples

### Create a task

Created tasks start at status `1`.

```http
POST /api/tasks
Content-Type: application/json

{
  "taskType": "Development",
  "description": "Implement workflow API",
  "assignedToUserId": 1,
  "customDataJson": "{}"
}
```

Response body is a `TaskDetailsDto`:

```json
{
  "id": 42,
  "taskType": "Development",
  "currentStatus": 1,
  "assignedToUserId": 1,
  "description": "Implement workflow API",
  "createdAt": "2026-05-26T12:00:00Z",
  "updatedAt": "2026-05-26T12:00:00Z",
  "assignedToUser": { "id": 1, "name": "דן כהן", "email": "dan@example.com" },
  "customDataJson": "{}"
}
```

### Move forward and reassign

`newDataJson` must be a JSON string inside the request JSON.

```http
POST /api/tasks/42/change-status
Content-Type: application/json

{
  "newStatus": 2,
  "nextAssignedToUserId": 2,
  "newDataJson": "{\"specification\":\"Build status workflow endpoints\"}"
}
```

Success:

```json
{
  "success": true,
  "message": "סטטוס עודכן בהצלחה ל-2",
  "newStatus": 2,
  "task": {
    "id": 42,
    "taskType": "Development",
    "currentStatus": 2,
    "assignedToUserId": 2,
    "description": "Implement workflow API",
    "customDataJson": "{\"specification\":\"Build status workflow endpoints\"}"
  }
}
```

### Roll back

```http
POST /api/tasks/42/change-status
Content-Type: application/json

{
  "newStatus": 1,
  "nextAssignedToUserId": 1,
  "newDataJson": "{}"
}
```

Rollback replaces `CustomDataJson` with the supplied payload.

### Close

Only call close after the task reaches its handler final status.

```http
POST /api/tasks/42/close
Content-Type: application/json

{
  "finalNotes": "Released in 1.0.0"
}
```

Success response includes the updated task at status `99`.

## Query responses

`GET /api/tasks`, `GET /api/tasks/byType/{taskType}`,
`GET /api/tasks/user/{userId}`, and `GET /api/users/{id}/tasks` return
`PagedResult<TaskSummaryDto>`:

```json
{
  "items": [
    {
      "id": 42,
      "taskType": "Development",
      "currentStatus": 2,
      "assignedToUserId": 2,
      "description": "Implement workflow API",
      "createdAt": "2026-05-26T12:00:00Z",
      "updatedAt": "2026-05-26T12:10:00Z",
      "assignedToUser": { "id": 2, "name": "רות לוי", "email": "ruth@example.com" }
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

List endpoints omit `customDataJson`; use `GET /api/tasks/{id}` for details.
`PageRequest` normalizes `page < 1` to `1`, `pageSize < 1` to `20`, and caps
`pageSize` at `100`.

## Developer checklist

- Add workflow behavior to `TaskWorkflowService` only when it is a cross-type
  invariant.
- Add task-type payload rules to an `ITaskHandler`.
- Keep request-shape validation in `Validation/*RequestValidators.cs`.
- Use `WorkflowConstants.CreatedStatus` and `WorkflowConstants.ClosedStatus`.
- Include reassignment behavior in tests for status changes.
- Test closed-task behavior for any new mutation endpoint.

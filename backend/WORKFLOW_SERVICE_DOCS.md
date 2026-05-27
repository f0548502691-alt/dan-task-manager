# Task Workflow API and Request Contracts

This document covers the public task workflow API and the backend codepaths that enforce it.

## Architecture

```text
HTTP clients
  -> Controllers/TasksController.cs
  -> Contracts/Requests/Tasks/*
  -> FluentValidation validators
  -> MediatR commands/queries
  -> TaskApplicationService / TaskWorkflowService
  -> rule providers (metadata first, handler fallback)
  -> EF Core
```

Key implementation points:

- Request models live under `backend/Contracts/Requests`, not inside controllers.
- `TasksController` validates requests, converts `customFields` objects into JSON strings, and delegates through MediatR.
- `TaskApplicationService` owns task creation and read DTO mapping.
- `TaskWorkflowService` owns status movement, assignment changes, close behavior, and workflow validation.
- `MetadataTaskWorkflowRuleProvider` has priority `0`; `HandlerTaskWorkflowRuleProvider` has priority `100`. Metadata-backed task types win over handler fallback.
- Errors are surfaced by throwing API exceptions and serialized by `GlobalExceptionMiddleware`; see `API_ERROR_CODES.md`.

## Public request models

| Request | File | Notes |
|---------|------|-------|
| `PaginationQuery` | `Contracts/Requests/Common/PaginationQuery.cs` | Query string model for paged list endpoints. Defaults: `page=1`, `pageSize=20`; service layer caps page size at `100`. |
| `CreateTaskRequest` | `Contracts/Requests/Tasks/CreateTaskRequest.cs` | `taskType`, `description`, `assignedToUserId`, optional `customFields` object. |
| `ChangeStatusWorkflowRequest` | `Contracts/Requests/Tasks/ChangeStatusWorkflowRequest.cs` | `newStatus`, `nextAssignedToUserId`, required `customFields` object. |
| `CloseTaskRequest` | `Contracts/Requests/Tasks/CloseTaskRequest.cs` | `nextAssignedToUserId`, `finalNotes`. |
| `UpdateTaskRequest` | `Contracts/Requests/Tasks/UpdateTaskRequest.cs` | Optional `description`. |
| `UpsertTaskTypeRequest` | `Contracts/Requests/TaskTypes/UpsertTaskTypeRequest.cs` | Metadata task type creation/update. |
| `UpsertTaskTypeFieldRequest` | `Contracts/Requests/TaskTypes/UpsertTaskTypeFieldRequest.cs` | Metadata field validation rules. |

`customDataJson` is the storage field on `BaseTask`. The public HTTP contract uses `customFields` as a JSON object.

## Workflow rules

Constants are defined in `Domain/WorkflowConstants.cs`:

| Name | Value | Meaning |
|------|-------|---------|
| `CreatedStatus` | `1` | Initial status for newly created tasks. |
| `ClosedStatus` | `99` | Terminal closed state. Closed tasks are immutable. |

Movement rules enforced by `TaskWorkflowService.ChangeStatusAsync`:

- A closed task cannot change status.
- `nextAssignedToUserId` must identify an existing user.
- `customFields` must serialize to a valid JSON object.
- Direct movement to status `99` is rejected; use `POST /api/tasks/{id}/close`.
- `newStatus` must be `>= 1`.
- Forward movement must be exactly `+1`.
- Backward movement may move to any lower status down to `1`.
- Re-submitting the current status is invalid.
- If the task is at or beyond its final status, it cannot move forward.
- Type-specific validation is evaluated after generic movement validation.

Close rules enforced by `TaskWorkflowService.CloseTaskAsync`:

- The task must exist and must not already be closed.
- `nextAssignedToUserId` must identify an existing user.
- The task type must resolve to a final status.
- The task can close only when `currentStatus == finalStatus`.
- Close sets `currentStatus` to `99`, updates `assignedToUserId`, and adds `finalNotes` plus `closedAt` into stored custom data.

## Response DTOs

List endpoints return `PagedResult<TaskSummaryDto>`:

```json
{
  "items": [
    {
      "id": 1,
      "taskType": "Procurement",
      "currentStatus": 2,
      "assignedToUserId": 1,
      "description": "Buy server parts",
      "createdAt": "2026-05-25T10:00:00Z",
      "updatedAt": "2026-05-25T10:05:00Z",
      "assignedToUser": {
        "id": 1,
        "name": "Dan",
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

Task detail responses return `TaskDetailsDto`, which includes `customFields`:

```json
{
  "id": 1,
  "taskType": "Procurement",
  "currentStatus": 2,
  "assignedToUserId": 1,
  "description": "Buy server parts",
  "createdAt": "2026-05-25T10:00:00Z",
  "updatedAt": "2026-05-25T10:05:00Z",
  "assignedToUser": null,
  "customFields": {
    "prices": ["5000", "4800"]
  }
}
```

## REST endpoints

### Create task

```http
POST /api/tasks
Content-Type: application/json

{
  "taskType": "Procurement",
  "description": "Buy server parts",
  "assignedToUserId": 1,
  "customFields": {}
}
```

Successful response: `201 Created` with `TaskDetailsDto`.

Important constraints:

- `taskType` and `description` are required.
- `assignedToUserId` must be greater than `0` and must refer to an existing user.
- `customFields` may be omitted; when present it must be a JSON object.
- Created tasks start at status `1`.
- Unsupported task types fail with `task_type_validation_failed`; the supported task types are included in the error message text.

### Change status

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

Successful response:

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
    "description": "Buy server parts",
    "createdAt": "2026-05-25T10:00:00Z",
    "updatedAt": "2026-05-25T10:05:00Z",
    "assignedToUser": null,
    "customFields": {
      "prices": ["5000", "4800"]
    }
  }
}
```

Use `customFields`, not `newDataJson`. The controller converts the object to stored JSON before calling `ChangeTaskStatusCommand`.

### Close task

```http
POST /api/tasks/1/close
Content-Type: application/json

{
  "nextAssignedToUserId": 2,
  "finalNotes": "Completed successfully"
}
```

Successful response:

```json
{
  "success": true,
  "message": "משימה סגורה בהצלחה",
  "task": {
    "id": 1,
    "taskType": "Procurement",
    "currentStatus": 99,
    "assignedToUserId": 2,
    "description": "Buy server parts",
    "createdAt": "2026-05-25T10:00:00Z",
    "updatedAt": "2026-05-25T10:10:00Z",
    "assignedToUser": null,
    "customFields": {
      "finalNotes": "Completed successfully",
      "closedAt": "2026-05-25T10:10:00.0000000Z"
    }
  }
}
```

The task must already be at the task type's final status before it can close.

### List and detail reads

```http
GET /api/tasks?page=1&pageSize=20
GET /api/tasks/user/1?page=1&pageSize=20
GET /api/tasks/byType/Procurement?page=1&pageSize=20
GET /api/tasks/1
```

- List endpoints return `PagedResult<TaskSummaryDto>` and do not include `customFields`.
- Detail reads return `TaskDetailsDto` and include `customFields`.
- User task reads verify the user exists before returning a page.

### Update and delete

```http
PUT /api/tasks/1
Content-Type: application/json

{ "description": "Updated description" }
```

```http
DELETE /api/tasks/1
```

Both operations reject closed tasks through the same workflow immutability rule.

## Task type metadata endpoints

Task type metadata request models also moved to `Contracts/Requests/TaskTypes`.

```http
POST /api/task-types
Content-Type: application/json

{
  "taskType": "QA",
  "displayName": "QA",
  "finalStatus": 3,
  "isActive": true
}
```

```http
POST /api/task-types/QA/fields
Content-Type: application/json

{
  "field": "testCases",
  "type": "array",
  "required": true,
  "minItems": 1,
  "elementType": "string",
  "appliesFromStatus": 2,
  "appliesToStatus": 3
}
```

Validation failures from these endpoints use `task_type_validation_failed` or `task_type_field_validation_failed`.

## Developer checklist

- Put new public request bodies under `backend/Contracts/Requests/<Area>`.
- Add or update FluentValidation validators under `backend/Validation`.
- Keep controller actions thin: validate, translate request models to commands/queries, throw `ApiException` subclasses for handled failures.
- Use `customFields` in public HTTP examples and client payloads. Reserve `customDataJson` for persistence/internal normalization.
- Update `API_ERROR_CODES.md` whenever a new stable error code is introduced.

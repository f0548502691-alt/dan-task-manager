# API Error Handling Reference

This API returns a consistent JSON shape for handled errors:

```json
{
  "error": "TaskType נדרש",
  "code": "validation_failed"
}
```

- `error` is the human-readable message to display. It may be localized and should not be used as a stable branch key.
- `code` is the machine-readable category. It is optional on older responses, but all handled errors emitted by `GlobalExceptionMiddleware` include it.

## Backend flow

| Codepath | Responsibility |
|----------|----------------|
| `Domain/ApiException.cs` | Base exception carrying `StatusCode`, `Code`, and message. |
| `Domain/ApiValidationException.cs` | Maps validation-style failures to HTTP 400. Default code: `validation_failed`. |
| `Domain/WorkflowValidationException.cs` | Workflow/business validation failure. Inherits from `ApiValidationException` with code `workflow_validation_failed`. |
| `Domain/ApiNotFoundException.cs` | Resource lookup failure. Maps to HTTP 404 with code `not_found`. |
| `Middleware/GlobalExceptionMiddleware.cs` | Catches `ApiException`, logs a warning, and serializes `{ error, code }`. Unhandled exceptions become HTTP 500 with code `internal_server_error`. |
| `Controllers/*Controller.cs` | Throws API exceptions instead of returning ad hoc `BadRequest`/`NotFound` payloads. |

`Program.cs` registers the middleware before HTTPS redirection and controller mapping:

```csharp
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseHttpsRedirection();
app.MapControllers();
```

## Status and code matrix

| Scenario | Status | Code | Source |
|----------|--------|------|--------|
| FluentValidation request failures | 400 | `validation_failed` | `TasksController` validators for create/change/close requests |
| Create task unsupported type | 400 | `task_type_validation_failed` | `TasksController.CreateTask` |
| Create task application failure | 400 | `task_creation_failed` | `TasksController.CreateTask` |
| Workflow movement, assignment, final-status, or closed-task failure | 400 | `workflow_validation_failed` | `WorkflowValidationException` |
| Task type upsert failure | 400 | `task_type_validation_failed` | `TaskTypesController.UpsertTaskType` |
| Task type field upsert failure | 400 | `task_type_field_validation_failed` | `TaskTypesController.UpsertTaskTypeField` |
| Missing task, user, or task type schema | 404 | `not_found` | `ApiNotFoundException` |
| Unexpected server exception | 500 | `internal_server_error` | `GlobalExceptionMiddleware` fallback |

## Examples

### Request validation failure

```http
POST /api/tasks/1/change-status
Content-Type: application/json

{
  "newStatus": 2,
  "nextAssignedToUserId": 1,
  "customFields": []
}
```

```json
{
  "error": "CustomFields חייב להיות אובייקט JSON",
  "code": "validation_failed"
}
```

`ChangeStatusWorkflowRequest` requires:

- `newStatus > 0`
- `nextAssignedToUserId > 0`
- `customFields` present and shaped as a JSON object

### Workflow validation failure

```http
POST /api/tasks/1/change-status
Content-Type: application/json

{
  "newStatus": 3,
  "nextAssignedToUserId": 1,
  "customFields": {}
}
```

When the task is currently at status `1`, forward movement must be exactly `+1`, so the response is:

```json
{
  "error": "תנועה קדימה חייבת להיות בדיוק ב-1 סטטוס. סטטוס נוכחי: 1, מבוקש: 3",
  "code": "workflow_validation_failed"
}
```

### Resource not found

```http
GET /api/tasks/999999
```

```json
{
  "error": "משימה לא נמצאה",
  "code": "not_found"
}
```

### Unknown task type on create

```http
POST /api/tasks
Content-Type: application/json

{
  "taskType": "Unknown",
  "description": "Example",
  "assignedToUserId": 1,
  "customFields": {}
}
```

```json
{
  "error": "סוג משימה לא נתמך: Unknown. Supported task types: Analysis, Development, Procurement, Testing",
  "code": "task_type_validation_failed"
}
```

The supported type names are generated from registered handlers plus metadata-backed task types and are included in the message text. The response no longer includes a separate `supportedTaskTypes` property.

## Frontend handling

The Angular client consumes the same shape through `frontend/src/app/core/error-message.utils.ts`:

1. Prefer `payload.error` when the backend returns an `ApiErrorResponse`.
2. Fall back to a non-empty string payload.
3. Fall back to `HttpErrorResponse.message`.
4. Use `"Unexpected server error."` or `"Unexpected error while communicating with the server."` when no better message exists.

`httpErrorInterceptor` writes the extracted message to `AppErrorService` and rethrows `Error(message)`. `TaskService` also stores the message in its feature-level `error` signal so screens can show local and global error states consistently.

## Extension rules

- Throw `ApiException` subclasses for handled API errors so the middleware preserves the intended status and `code`.
- Do not return anonymous `{ error = ... }` payloads directly from controllers.
- Add new request validation to `backend/Validation/*RequestValidators.cs`; keep public request models in `backend/Contracts/Requests`.
- Display `error` to users. Use `code` only for optional branching or analytics, and keep the UI tolerant of `code` being absent.
- Add or update examples in this file when a new stable error code is introduced.

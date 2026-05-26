# API Errors and Response Codes

The API returns two error shapes:

1. Controller validation/application failures:

   ```json
   { "error": "Description נדרש" }
   ```

2. Workflow failures handled by `GlobalExceptionMiddleware`:

   ```json
   {
     "error": "משימה סגורה - לא ניתן לשנות סטטוס",
     "code": "workflow_validation_failed"
   }
   ```

Unhandled exceptions are also formatted by the middleware:

```json
{
  "error": "אירעה שגיאה בלתי צפויה בשרת",
  "code": "internal_server_error"
}
```

## HTTP status codes

| Code | Meaning | Typical source |
|------|---------|----------------|
| `200 OK` | Query or workflow action succeeded | Controllers |
| `201 Created` | Task/user created | Controllers |
| `204 No Content` | Update/delete succeeded | Controllers |
| `400 Bad Request` | Request validation, application validation, or workflow validation failed | FluentValidation, application services, workflow middleware |
| `404 Not Found` | Requested task/user was not found | Controllers |
| `500 Internal Server Error` | Unexpected server failure | Global exception middleware |

## Request validation failures

These are returned directly by controllers after FluentValidation runs. Multiple
messages are joined with `; `.

### Create task

Rules from `CreateTaskRequestValidator`:

- `taskType` is required.
- `description` is required.
- `assignedToUserId` must be greater than `0`.
- `customDataJson`, when provided, must be valid JSON.

Example:

```http
POST /api/tasks
Content-Type: application/json

{
  "taskType": "",
  "description": "",
  "assignedToUserId": 0,
  "customDataJson": "{not json}"
}
```

```json
{
  "error": "TaskType נדרש; Description נדרש; AssignedToUserId חייב להיות גדול מ-0; CustomDataJson חייב להיות JSON תקין"
}
```

### Change status

Rules from `ChangeStatusWorkflowRequestValidator`:

- `newStatus` must be greater than `0`.
- `nextAssignedToUserId` must be greater than `0`.
- `newDataJson` is required and must be valid JSON.

Example:

```http
POST /api/tasks/42/change-status
Content-Type: application/json

{
  "newStatus": 0,
  "nextAssignedToUserId": 0,
  "newDataJson": ""
}
```

```json
{
  "error": "NewStatus חייב להיות גדול מ-0; NextAssignedToUserId נדרש; NewDataJson נדרש; NewDataJson חייב להיות JSON תקין"
}
```

### Close task

`finalNotes` is required:

```json
{ "error": "FinalNotes נדרש" }
```

### Create user

Rules from `CreateUserRequestValidator`:

- `name` is required and at most 255 characters.
- `email` is required, must be a valid email address, and at most 255 characters.

Example:

```json
{
  "error": "Name נדרש; Email לא תקין"
}
```

## Application validation failures

Application services return 400 directly for non-workflow business checks.

| Scenario | Example response |
|----------|------------------|
| Creating a task for a missing user | `{ "error": "משתמש לא קיים" }` |
| Creating a task with unsupported `taskType` | `{ "error": "סוג משימה לא נתמך: Unknown" }` |
| Creating a task with invalid JSON after normalization | `{ "error": "JSON לא תקין: ..." }` |
| Creating a duplicate user email | Service-specific duplicate email message |

## Workflow validation failures

Workflow failures are thrown as `WorkflowValidationException` by
`TasksController` and returned with `code: "workflow_validation_failed"`.

### Missing task

Status changes and close calls fail through workflow validation when the task is
not found:

```json
{
  "error": "משימה לא קיימת",
  "code": "workflow_validation_failed"
}
```

`GET`, `PUT`, and `DELETE` endpoints return 404 when they explicitly determine
that the task is missing.

### Closed task

Changing status:

```json
{
  "error": "משימה סגורה - לא ניתן לשנות סטטוס",
  "code": "workflow_validation_failed"
}
```

Updating or deleting a closed task:

```json
{
  "error": "משימה סגורה היא immutable ולא ניתן לעדכן אותה",
  "code": "workflow_validation_failed"
}
```

```json
{
  "error": "משימה סגורה היא immutable ולא ניתן למחוק אותה",
  "code": "workflow_validation_failed"
}
```

### Invalid movement

Forward jumps must be exactly `+1`:

```json
{
  "error": "תנועה קדימה חייבת להיות בדיוק ב-1 סטטוס. סטטוס נוכחי: 1, מבוקש: 3",
  "code": "workflow_validation_failed"
}
```

Same-status movement:

```json
{
  "error": "סטטוס חדש זהה לסטטוס הנוכחי",
  "code": "workflow_validation_failed"
}
```

Attempting to close through change-status:

```json
{
  "error": "סגירת משימה מתבצעת רק דרך CloseTask",
  "code": "workflow_validation_failed"
}
```

Attempting to move beyond a handler final status:

```json
{
  "error": "משימה כבר הגיעה לסטטוס סופי (3)",
  "code": "workflow_validation_failed"
}
```

### Reassignment and JSON checks

`TaskWorkflowService` validates data again even after controller validation:

```json
{
  "error": "המשתמש הבא לא קיים",
  "code": "workflow_validation_failed"
}
```

```json
{
  "error": "NewDataJson חייב להיות JSON תקין",
  "code": "workflow_validation_failed"
}
```

### Close from non-final status

Tasks can close only from their handler final status:

```json
{
  "error": "ניתן לסגור משימה מסוג Development רק מסטטוס סופי 4",
  "code": "workflow_validation_failed"
}
```

## Handler validation failures

Handler errors also return `code: "workflow_validation_failed"` because they are
part of the workflow result.

### Procurement

Status `2` requires exactly two non-empty price strings:

```json
{
  "error": "בסטטוס 2, נדרש שדה 'prices' המכיל מערך של 2 מחרוזות (מחירים)",
  "code": "workflow_validation_failed"
}
```

```json
{
  "error": "'prices' חייב להכיל בדיוק 2 מחרוזות, נמצאו 1",
  "code": "workflow_validation_failed"
}
```

Status `3` requires a non-empty `receipt` string:

```json
{
  "error": "בסטטוס 3, נדרש שדה 'receipt' המכיל מחרוזת של קבלה",
  "code": "workflow_validation_failed"
}
```

### Development

Status `2` requires `specification` with at least 10 characters:

```json
{
  "error": "'specification' חייב להכיל לפחות 10 תווים",
  "code": "workflow_validation_failed"
}
```

Status `3` requires a valid `branchName`:

```json
{
  "error": "שם הבראנץ' אינו תקין (לא יכול להכיל //, להסתיים ב-/, . או רווחים)",
  "code": "workflow_validation_failed"
}
```

Status `4` requires a string or numeric `versionNumber`:

```json
{
  "error": "בסטטוס 4, נדרש שדה 'versionNumber' המכיל מספר גרסה",
  "code": "workflow_validation_failed"
}
```

## Success response examples

### Status changed

```json
{
  "success": true,
  "message": "סטטוס עודכן בהצלחה ל-2",
  "newStatus": 2,
  "task": {
    "id": 42,
    "taskType": "Procurement",
    "currentStatus": 2,
    "assignedToUserId": 2,
    "description": "רכישת חומרים",
    "createdAt": "2026-05-26T12:00:00Z",
    "updatedAt": "2026-05-26T12:05:00Z",
    "assignedToUser": { "id": 2, "name": "רות לוי", "email": "ruth@example.com" },
    "customDataJson": "{\"prices\":[\"5000\",\"4800\"]}"
  }
}
```

### Task list

List endpoints return `PagedResult<TaskSummaryDto>` and omit `customDataJson`:

```json
{
  "items": [
    {
      "id": 42,
      "taskType": "Procurement",
      "currentStatus": 2,
      "assignedToUserId": 2,
      "description": "רכישת חומרים",
      "createdAt": "2026-05-26T12:00:00Z",
      "updatedAt": "2026-05-26T12:05:00Z",
      "assignedToUser": { "id": 2, "name": "רות לוי", "email": "ruth@example.com" }
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

## Troubleshooting checklist

- For 400 responses without `code`, check request DTO validation and application
  service checks.
- For 400 responses with `workflow_validation_failed`, inspect workflow movement,
  handler payload requirements, closed-task state, and reassignment user IDs.
- For list responses, do not expect `customDataJson`; call `GET /api/tasks/{id}`.
- For unexpected 500 responses, check application logs from
  `GlobalExceptionMiddleware`.

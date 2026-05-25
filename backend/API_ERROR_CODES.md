# API Response Codes & Error Messages

This reference covers the current controller, application-service, and workflow error behavior.

## HTTP Status Codes

| Code | Meaning | Where it occurs |
|------|---------|-----------------|
| `200 OK` | Successful read or workflow operation | GET endpoints, status change, close task |
| `201 Created` | Resource created | `POST /api/tasks`, `POST /api/users` |
| `204 No Content` | Resource updated/deleted without body | `PUT /api/tasks/{id}`, `DELETE /api/tasks/{id}` |
| `400 Bad Request` | Request or workflow validation failed | Missing fields, invalid JSON, movement/handler validation |
| `404 Not Found` | Resource missing on read/update/delete routes | Missing task or user on lookup routes |
| `500 Server Error` | Unhandled server error | Formatted by `GlobalExceptionMiddleware` |

## Error Response Shapes

Simple controller validation returns:

```json
{
  "error": "Description נדרש"
}
```

Workflow failures from `POST /api/tasks/{id}/change-status` and `POST /api/tasks/{id}/close` are thrown as `WorkflowValidationException` and formatted by `GlobalExceptionMiddleware`:

```json
{
  "error": "תנועה קדימה חייבת להיות בדיוק ב-1 סטטוס. סטטוס נוכחי: 1, מבוקש: 3",
  "code": "workflow_validation_failed"
}
```

## Workflow Validation Errors

| Scenario | Status | Message |
|----------|--------|---------|
| Invalid forward jump | `400` | `תנועה קדימה חייבת להיות בדיוק ב-1 סטטוס. סטטוס נוכחי: {current}, מבוקש: {requested}` |
| Same status | `400` | `סטטוס חדש זהה לסטטוס הנוכחי` |
| Negative status | `400` | `סטטוס לא יכול להיות שלילי` |
| Closed task | `400` | `משימה סגורה - לא ניתן לשנות סטטוס` |
| Already closed on close request | `400` | `משימה כבר סגורה` |
| Final status exceeded | `400` | `משימה כבר הגיעה לסטטוס סופי ({finalStatus})` |
| Task missing during workflow | `400` | `משימה לא קיימת` |
| Invalid status payload JSON | `400` | `NewDataJson חייב להיות JSON תקין` |

Example invalid JSON request:

```http
POST /api/tasks/1/change-status
Content-Type: application/json

{
  "newStatus": 2,
  "newDataJson": "{not valid json}"
}
```

```json
{
  "error": "NewDataJson חייב להיות JSON תקין",
  "code": "workflow_validation_failed"
}
```

## Handler Validation Errors

These errors are returned through the workflow error shape above because handlers run inside `TaskWorkflowService.ChangeStatusAsync`.

### Procurement

| Scenario | Message |
|----------|---------|
| Missing prices at status 2 | `בסטטוס 2, נדרש שדה 'prices' המכיל מערך של 2 מחרוזות (מחירים)` |
| Prices is not an array | `'prices' חייב להיות מערך` |
| Wrong price count | `'prices' חייב להכיל בדיוק 2 מחרוזות, נמצאו {count}` |
| Price is not a string | `כל המחירים חייבים להיות מחרוזות` |
| Empty price | `המחירים לא יכולים להיות ריקים` |
| Missing receipt at status 3 | `בסטטוס 3, נדרש שדה 'receipt' המכיל מחרוזת של קבלה` |
| Receipt is not a string | `'receipt' חייב להיות מחרוזת` |
| Empty receipt | `'receipt' לא יכול להיות ריק` |

### Development

| Scenario | Message |
|----------|---------|
| Missing specification at status 2 | `בסטטוס 2, נדרש שדה 'specification' המכיל טקסט אפיון` |
| Specification is not a string | `'specification' חייב להיות מחרוזת` |
| Short specification | `'specification' חייב להכיל לפחות 10 תווים` |
| Missing branch at status 3 | `בסטטוס 3, נדרש שדה 'branchName' המכיל שם הבראנץ'` |
| Branch is not a string | `'branchName' חייב להיות מחרוזת` |
| Empty branch | `'branchName' לא יכול להיות ריק` |
| Invalid branch syntax | `שם הבראנץ' אינו תקין (לא יכול להכיל //, להסתיים ב-/, . או רווחים)` |
| Missing version at status 4 | `בסטטוס 4, נדרש שדה 'versionNumber' המכיל מספר גרסה` |
| Version has invalid type | `'versionNumber' חייב להיות מחרוזת או מספר` |
| Empty version | `'versionNumber' לא יכול להיות ריק` |
| Version has non-numeric parts | `'versionNumber' חייב להיות בפורמט SemVer (לדוגמה: 1.0.0), קיבלנו: {value}` |

## Create/Update Validation Errors

| Endpoint | Scenario | Status | Response |
|----------|----------|--------|----------|
| `POST /api/tasks` | Missing `taskType` | `400` | `{ "error": "TaskType נדרש" }` |
| `POST /api/tasks` | Missing `description` | `400` | `{ "error": "Description נדרש" }` |
| `POST /api/tasks` | Assigned user does not exist | `400` | `{ "error": "משתמש לא קיים" }` |
| `POST /api/tasks` | Invalid `customDataJson` | `400` | `{ "error": "JSON לא תקין: ..." }` |
| `POST /api/users` | Missing `name` | `400` | `{ "error": "Name נדרש" }` |
| `POST /api/users` | Missing `email` | `400` | `{ "error": "Email נדרש" }` |
| `POST /api/users` | Duplicate email | `400` | `{ "error": "כתובת האימייל כבר קיימת במערכת" }` |
| `POST /api/tasks/{id}/close` | Missing `finalNotes` | `400` | `{ "error": "FinalNotes נדרש" }` |
| `PUT /api/tasks/{id}` | Task missing | `404` | Empty body |
| `DELETE /api/tasks/{id}` | Task missing | `404` | Empty body |

Unknown task types are allowed at creation time. `TaskApplicationService` logs a warning when no handler is registered; later workflow changes for that task use only the shared movement rules because there is no handler-specific validation.

## Not Found Behavior

| Endpoint | Missing resource response |
|----------|---------------------------|
| `GET /api/tasks/{id}` | `404` with empty body |
| `GET /api/tasks/user/{userId}` | `404` with plain text `משתמש לא קיים` |
| `GET /api/users/{id}` | `404` with empty body |
| `GET /api/users/{id}/tasks` | `404` with plain text `משתמש לא קיים` |

## Success Response Examples

### Status Changed

```json
{
  "success": true,
  "message": "סטטוס עודכן בהצלחה ל-2",
  "newStatus": 2,
  "task": {
    "id": 1,
    "taskType": "Procurement",
    "currentStatus": 2,
    "customDataJson": "{\"prices\":[\"5000\",\"4800\"]}"
  }
}
```

### Task Closed

The close endpoint does not return a top-level `newStatus`; the updated task contains `currentStatus: 99`.

```json
{
  "success": true,
  "message": "משימה סגורה בהצלחה",
  "task": {
    "id": 1,
    "currentStatus": 99,
    "customDataJson": "{\"finalNotes\":\"Done\",\"closedAt\":\"2026-05-25T10:10:00.0000000Z\"}"
  }
}
```

## Troubleshooting Checklist

1. For workflow `400` responses, check both `error` and `code`.
2. Verify `newDataJson` is a JSON string containing valid JSON, not an object.
3. Move forward one status at a time; rollback may target any lower status.
4. Confirm the task is not closed (`currentStatus != 99`).
5. For handler-specific failures, compare the payload with the required fields for the task type and target status.

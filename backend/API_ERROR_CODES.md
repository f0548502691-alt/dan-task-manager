# API error and response reference

This file documents the current error contracts used by the task workflow API.
The source of truth is `Controllers/TasksController.cs`,
`Validation/TaskRequestValidators.cs`, `Services/TaskWorkflowService.cs`, and
`Middleware/GlobalExceptionMiddleware.cs`.

## HTTP status codes

| Code | When it is used |
| --- | --- |
| `200 OK` | Successful reads, status changes, and closes |
| `201 Created` | Successful task creation |
| `204 No Content` | Successful description update or delete |
| `400 Bad Request` | Request validation, workflow validation, unsupported task types |
| `404 Not Found` | Missing task or missing user on `GET /api/tasks/user/{id}` |
| `500 Internal Server Error` | Unhandled server failure |

Validation errors are usually returned as `{ "error": "..." }`. Workflow errors
thrown through `GlobalExceptionMiddleware` also include a stable `code` field.

```json
{
  "error": "תנועה קדימה חייבת להיות בדיוק ב-1 סטטוס. סטטוס נוכחי: 1, מבוקש: 3",
  "code": "workflow_validation_failed"
}
```

## Request validation errors

### Create task

```http
POST /api/tasks
```

Required fields:

- `taskType`
- `description`
- `assignedToUserId` greater than `0`
- optional `customFields`, if present, must be a JSON object

Examples:

```json
{ "error": "TaskType נדרש" }
{ "error": "AssignedToUserId חייב להיות גדול מ-0" }
{ "error": "CustomFields חייב להיות אובייקט JSON" }
```

Unsupported task types are checked against
`WorkflowConstants.SupportedTaskTypes` (`Development`, `Procurement`):

```json
{
  "error": "סוג משימה לא נתמך: Analysis",
  "supportedTaskTypes": ["Development", "Procurement"]
}
```

### Change status

```http
POST /api/tasks/{id}/change-status
```

Required fields:

- `newStatus` greater than `0`
- `nextAssignedToUserId` greater than `0`
- `customFields` as a JSON object

Examples:

```json
{ "error": "NewStatus חייב להיות גדול מ-0" }
{ "error": "NextAssignedToUserId נדרש" }
{ "error": "CustomFields נדרש" }
{ "error": "CustomFields חייב להיות אובייקט JSON" }
```

### Close task

```http
POST /api/tasks/{id}/close
```

`finalNotes` is required:

```json
{ "error": "FinalNotes נדרש" }
```

## Workflow validation errors

| Scenario | Example message |
| --- | --- |
| Task does not exist | `משימה לא קיימת` |
| Task is already closed | `משימה סגורה - לא ניתן לשנות סטטוס` |
| Next assignee does not exist | `המשתמש הבא לא קיים` |
| `customFields` is not valid object JSON | `customFields חייב להיות אובייקט JSON תקין` |
| Task type is not supported | `סוג משימה לא נתמך: {taskType}` |
| Closing through status change | `סגירת משימה מתבצעת רק דרך CloseTask` |
| Status below created | `סטטוס חייב להיות 1 ומעלה` |
| Forward jump skips a status | `תנועה קדימה חייבת להיות בדיוק ב-1 סטטוס...` |
| Same status requested | `סטטוס חדש זהה לסטטוס הנוכחי` |
| Already at final status | `משימה כבר הגיעה לסטטוס סופי ({finalStatus})` |
| Close before final status | `ניתן לסגור משימה מסוג {taskType} רק מסטטוס סופי {finalStatus}` |

## Type-specific validation errors

Metadata-backed validation messages use field names and status numbers. The
current seeded rules are:

| Task type | Status | Missing or invalid data |
| --- | ---: | --- |
| `Procurement` | `2` | `prices` must be an array with exactly two string items |
| `Procurement` | `3` | `receipt` must be a non-empty string |
| `Development` | `2` | `specification` must be a string with at least 10 characters |
| `Development` | `3` | `branchName` must pass the `valid_git_branch` pattern |
| `Development` | `4` | `versionNumber` must pass the `semantic_version` pattern |

Examples:

```json
{ "error": "בסטטוס 2, נדרש שדה 'prices'", "code": "workflow_validation_failed" }
{ "error": "השדה 'prices' חייב להכיל בדיוק 2 פריטים", "code": "workflow_validation_failed" }
{ "error": "השדה 'branchName' אינו שם branch תקין", "code": "workflow_validation_failed" }
{ "error": "השדה 'versionNumber' חייב להיות בפורמט גרסה תקין (לדוגמה: 1.0.0)", "code": "workflow_validation_failed" }
```

## Successful response shapes

### Task created

`POST /api/tasks` returns `TaskDetailsDto` and includes `customFields`.

```json
{
  "id": 1,
  "taskType": "Development",
  "currentStatus": 1,
  "assignedToUserId": 2,
  "description": "Implement login flow",
  "createdAt": "2026-05-27T09:00:00Z",
  "updatedAt": "2026-05-27T09:00:00Z",
  "customFields": {},
  "assignedToUser": {
    "id": 2,
    "name": "רות לוי",
    "email": "ruth@example.com"
  }
}
```

### Status changed

```json
{
  "success": true,
  "message": "סטטוס עודכן בהצלחה ל-2",
  "newStatus": 2,
  "task": {
    "id": 1,
    "taskType": "Development",
    "currentStatus": 2,
    "assignedToUserId": 3,
    "customFields": {
      "specification": "Detailed implementation notes"
    }
  }
}
```

### User tasks retrieved

`GET /api/tasks/user/{userId}` returns a `PagedResult<TaskSummaryDto>`. Summary
items do not include `customFields`; use `GET /api/tasks/{id}` for detail data.

```json
{
  "items": [
    {
      "id": 1,
      "taskType": "Development",
      "currentStatus": 2,
      "assignedToUserId": 3,
      "description": "Implement login flow",
      "createdAt": "2026-05-27T09:00:00Z",
      "updatedAt": "2026-05-27T09:05:00Z",
      "assignedToUser": null
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

# 🔔 API Response Codes & Error Messages

## HTTP Status Codes

| Code | Meaning | Example |
|------|---------|---------|
| **200 OK** | ✅ Success | Status changed, task closed, task retrieved |
| **201 Created** | ✅ Resource created | Task created successfully |
| **400 Bad Request** | ❌ Invalid request | Validation failed, invalid movement |
| **404 Not Found** | ❌ Resource missing | Task not found, user not found |
| **500 Server Error** | ❌ Server issue | Database error, unhandled exception |

---

## Response shape

- Request validation failures return `400 Bad Request` with `{ "error": "..." }`.
- Workflow business-rule failures are thrown as `WorkflowValidationException` and returned by
  `GlobalExceptionMiddleware` as `{ "error": "...", "code": "workflow_validation_failed" }`.
- Unhandled server errors return `{ "error": "אירעה שגיאה בלתי צפויה בשרת", "code": "internal_server_error" }`.

Current task workflow requests use `customFields` as an object and require `nextAssignedToUserId`
for both status changes and close operations:

```json
{
  "newStatus": 2,
  "nextAssignedToUserId": 1,
  "customFields": { "prices": ["5000", "4800"] }
}
```

```json
{
  "nextAssignedToUserId": 1,
  "finalNotes": "Done"
}
```

---

## Error Messages

### Movement Validation Errors

#### ❌ Forward Movement More Than +1
```
Status: 400 Bad Request

Response:
{
  "error": "תנועה קדימה חייבת להיות בדיוק ב-1 סטטוס. סטטוס נוכחי: 1, מבוקש: 3"
}
```

#### ❌ Same Status (No Change)
```
Status: 400 Bad Request

Response:
{
  "error": "סטטוס חדש זהה לסטטוס הנוכחי",
  "code": "workflow_validation_failed"
}
```

#### ❌ Closed Task (Status 99)
```
Status: 400 Bad Request

Response:
{
  "error": "משימה סגורה - לא ניתן לשנות סטטוס",
  "code": "workflow_validation_failed"
}
```

#### ❌ Final Status Exceeded
```
Status: 400 Bad Request

Response:
{
  "error": "משימה כבר הגיעה לסטטוס סופי (3)",
  "code": "workflow_validation_failed"
}
```

#### ❌ Status Below CreatedStatus
```
Status: 400 Bad Request

Response:
{
  "error": "סטטוס חייב להיות 1 ומעלה",
  "code": "workflow_validation_failed"
}
```

#### ❌ Closing Through Change Status
```
Status: 400 Bad Request

Response:
{
  "error": "סגירת משימה מתבצעת רק דרך CloseTask",
  "code": "workflow_validation_failed"
}
```

---

### Assignment and Payload Validation Errors

#### ❌ Missing Next Assignee
```
Status: 400 Bad Request

Response:
{
  "error": "NextAssignedToUserId נדרש"
}
```

#### ❌ Next Assignee Does Not Exist
```
Status: 400 Bad Request

Response:
{
  "error": "המשתמש הבא לא קיים",
  "code": "workflow_validation_failed"
}
```

#### ❌ Missing or Non-Object Custom Fields
```
Status: 400 Bad Request

Response:
{
  "error": "CustomFields נדרש"
}
```

Or:

```
Status: 400 Bad Request

Response:
{
  "error": "CustomFields חייב להיות אובייקט JSON"
}
```

---

### Task-Type Field Validation Errors

#### ❌ Procurement - Missing Prices
```
Status: 400 Bad Request

Response:
{
  "error": "בסטטוס 2, נדרש שדה 'prices'",
  "code": "workflow_validation_failed"
}
```

#### ❌ Procurement - Invalid Price Count
```
Status: 400 Bad Request

Response:
{
  "error": "השדה 'prices' חייב להכיל בדיוק 2 פריטים",
  "code": "workflow_validation_failed"
}
```

#### ❌ Procurement - Empty Price
```
Status: 400 Bad Request

Response:
{
  "error": "כל הערכים בשדה 'prices' חייבים להיות מחרוזות לא ריקות",
  "code": "workflow_validation_failed"
}
```

#### ❌ Procurement - Missing Receipt
```
Status: 400 Bad Request

Response:
{
  "error": "השדה 'receipt' לא יכול להיות ריק",
  "code": "workflow_validation_failed"
}
```

#### ❌ Development - Missing Specification
```
Status: 400 Bad Request

Response:
{
  "error": "בסטטוס 2, נדרש שדה 'specification'",
  "code": "workflow_validation_failed"
}
```

#### ❌ Development - Short Specification
```
Status: 400 Bad Request

Response:
{
  "error": "השדה 'specification' חייב להכיל לפחות 10 תווים",
  "code": "workflow_validation_failed"
}
```

#### ❌ Development - Invalid Branch Name
```
Status: 400 Bad Request

Response:
{
  "error": "השדה 'branchName' אינו שם branch תקין",
  "code": "workflow_validation_failed"
}
```

#### ❌ Development - Invalid Version
```
Status: 400 Bad Request

Response:
{
  "error": "השדה 'versionNumber' חייב להיות בפורמט גרסה תקין (לדוגמה: 1.0.0)",
  "code": "workflow_validation_failed"
}
```

---

### Task Type Metadata Errors

The metadata API (`POST /api/task-types`, `POST /api/task-types/{taskType}/fields`) enforces the
same status range used by workflow transitions.

#### ❌ Missing or Out-of-Range Final Status
```
Status: 400 Bad Request

Response:
{
  "error": "FinalStatus is required"
}
```

Or:

```
{
  "error": "FinalStatus must be greater than or equal to 1"
}
```

Or:

```
{
  "error": "FinalStatus must be less than 99"
}
```

#### ❌ Field Rule Outside Workflow Range
```
Status: 400 Bad Request

Response:
{
  "error": "AppliesFromStatus must be greater than or equal to 1"
}
```

Or:

```
{
  "error": "AppliesToStatus cannot be greater than FinalStatus (3)"
}
```

---

### Resource Not Found Errors

#### ❌ Task Not Found
```
Status: 404 Not Found

Response:
{
  "error": "משימה עם ID 999 לא נמצאה"
}
```

#### ❌ User Not Found
```
Status: 400 Bad Request

Response:
{
  "error": "משתמש לא קיים"
}
```

---

### Validation Errors (Create/Update)

#### ❌ Invalid Task Type
```
Status: 400 Bad Request

Response:
{
  "error": "TaskType נדרש"
}
```

#### ❌ Missing Description
```
Status: 400 Bad Request

Response:
{
  "error": "Description נדרש"
}
```

#### ❌ Unknown Task Type
```
Status: 400 Bad Request

Response:
{
  "error": "סוג משימה לא נתמך: Unknown",
  "supportedTaskTypes": ["Development", "Procurement"]
}
```

---

## Success Responses

### ✅ Status Changed Successfully
```
Status: 200 OK

Response:
{
  "success": true,
  "message": "סטטוס עודכן בהצלחה ל-2",
  "newStatus": 2,
  "task": {
    "id": 1,
    "taskType": "Procurement",
    "description": "רכישת חומרים",
    "currentStatus": 2,
    "assignedToUserId": 1,
    "customDataJson": "{\"prices\": [\"5000\", \"4800\"]}",
    "createdAt": "2026-05-25T10:00:00Z",
    "updatedAt": "2026-05-25T10:05:00Z"
  }
}
```

### ✅ Task Closed Successfully
```
Status: 200 OK

Response:
{
  "success": true,
  "message": "משימה סגורה בהצלחה",
  "task": {
    "id": 1,
    "taskType": "Procurement",
    "description": "רכישת חומרים",
    "currentStatus": 99,
    "assignedToUserId": 1,
    "customDataJson": "{\"prices\": [...], \"receipt\": \"...\", \"finalNotes\": \"משימה הושלמה בהצלחה\", \"closedAt\": \"2026-05-25T10:10:00Z\"}",
    "createdAt": "2026-05-25T10:00:00Z",
    "updatedAt": "2026-05-25T10:10:00Z"
  }
}
```

### ✅ Task Created
```
Status: 201 Created

Response:
{
  "id": 1,
  "taskType": "Procurement",
  "description": "רכישת חומרים",
  "currentStatus": 1,
  "assignedToUserId": 1,
  "customDataJson": "{}",
  "createdAt": "2026-05-25T10:00:00Z",
  "updatedAt": "2026-05-25T10:00:00Z"
}
```

### ✅ Task Retrieved
```
Status: 200 OK

Response:
{
  "id": 1,
  "taskType": "Procurement",
  "description": "רכישת חומרים",
  "currentStatus": 2,
  "assignedToUserId": 1,
  "customDataJson": "{\"prices\": [\"5000\", \"4800\"]}",
  "createdAt": "2026-05-25T10:00:00Z",
  "updatedAt": "2026-05-25T10:05:00Z"
}
```

### ✅ User Tasks Retrieved
```
Status: 200 OK

Response:
{
  "items": [
    {
      "id": 1,
      "taskType": "Procurement",
      "description": "רכישת חומרים",
      "currentStatus": 2,
      "assignedToUserId": 1
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

---

## Common Error Scenarios

### Scenario 1: Invalid Forward Jump

```
🔴 Request
POST /api/tasks/1/change-status
{ "newStatus": 3, "nextAssignedToUserId": 1, "customFields": {} }

Current Status: 1

🔴 Response
Status: 400 Bad Request
{
  "error": "תנועה קדימה חייבת להיות בדיוק ב-1 סטטוס. סטטוס נוכחי: 1, מבוקש: 3"
}

✅ Solution: Move to status 2 first
POST /api/tasks/1/change-status
{ "newStatus": 2, "nextAssignedToUserId": 1, "customFields": { "prices": ["5000", "4800"] } }
```

### Scenario 2: Missing Handler Data

```
🔴 Request
POST /api/tasks/1/change-status
{ "newStatus": 2, "nextAssignedToUserId": 1, "customFields": {} }

Current Task: Procurement, Status 1

🔴 Response
Status: 400 Bad Request
{
  "error": "'prices' חייב להכיל בדיוק 2 מחרוזות, לא נמצא שדה"
}

✅ Solution: Add required data
POST /api/tasks/1/change-status
{
  "newStatus": 2,
  "nextAssignedToUserId": 1,
  "customFields": { "prices": ["5000", "4800"] }
}
```

### Scenario 3: Task Already Closed

```
🔴 Request
POST /api/tasks/1/change-status
{ "newStatus": 1, "nextAssignedToUserId": 1, "customFields": {} }

Task Status: 99 (Closed)

🔴 Response
Status: 400 Bad Request
{
  "error": "משימה סגורה - לא ניתן לשנות סטטוס"
}

✅ Solution: Cannot be changed. Create new task if needed.
```

### Scenario 4: Invalid JSON Data

```
🔴 Request
POST /api/tasks/1/change-status
{
  "newStatus": 2,
  "nextAssignedToUserId": 1,
  "customFields": []
}

Current Task: Procurement, Status 1

🔴 Response
Status: 400 Bad Request
{
  "error": "CustomFields חייב להיות אובייקט JSON"
}

✅ Solution: Use valid JSON
POST /api/tasks/1/change-status
{
  "newStatus": 2,
  "nextAssignedToUserId": 1,
  "customFields": { "prices": ["5000", "4800"] }
}
```

---

## Testing Error Handling

### Unit Test Pattern
```csharp
[Fact]
public async Task ChangeStatus_InvalidMovement_ShouldReturnError()
{
    // Arrange
    var task = await _context.Tasks.FindAsync(1);
    task!.CurrentStatus = 1;

    // Act
    var result = await _service.ChangeStatusAsync(1, 3, 1, "{}");

    // Assert
    Assert.False(result.Success);
    Assert.Contains("בדיוק ב-1 סטטוס", result.Message);
}
```

### Postman Test Pattern
```javascript
// Test status code
pm.test("Status code is 400", function () {
    pm.response.to.have.status(400);
});

// Test error message
pm.test("Error message contains validation text", function () {
    pm.expect(pm.response.text()).to.include("תנועה קדימה");
});
```

---

## Summary

| Scenario | Status | Message | Action |
|----------|--------|---------|--------|
| ✅ Success | 200 | "סטטוס עודכן בהצלחה" | Proceed |
| ❌ Invalid jump | 400 | "בדיוק ב-1 סטטוס" | Move +1 first |
| ❌ Missing data | 400 | "לא נמצא שדה" | Add required data |
| ❌ Closed task | 400 | "משימה סגורה" | Cannot change |
| ❌ Not found | 404 | "לא נמצאה" | Create first |
| ❌ Bad request | 400 | Various | Check input |
| ❌ Server error | 500 | "שגיאת שרת" | Contact support |

---

**Always check the error message for specific guidance on what went wrong and how to fix it! 🔍**

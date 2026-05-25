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
  "error": "אותו סטטוס - לא ניתן לבקש שינוי לסטטוס זהה"
}
```

#### ❌ Closed Task (Status 99)
```
Status: 400 Bad Request

Response:
{
  "error": "משימה סגורה - לא ניתן לשנות סטטוס"
}
```

#### ❌ Final Status Exceeded
```
Status: 400 Bad Request

Response:
{
  "error": "משימה הגיעה לסטטוס סופי: 3. לא ניתן להעביר לסטטוס: 4"
}
```

---

### Handler Validation Errors

#### ❌ Procurement - Missing Prices
```
Status: 400 Bad Request

Response:
{
  "error": "'prices' חייב להכיל בדיוק 2 מחרוזות, לא נמצא שדה"
}
```

#### ❌ Procurement - Invalid Price Count
```
Status: 400 Bad Request

Response:
{
  "error": "'prices' חייב להכיל בדיוק 2 מחרוזות, נמצאו 1"
}
```

#### ❌ Procurement - Empty Price
```
Status: 400 Bad Request

Response:
{
  "error": "כל מחיר ב-'prices' חייב להיות מחרוזת לא ריקה"
}
```

#### ❌ Procurement - Missing Receipt
```
Status: 400 Bad Request

Response:
{
  "error": "'receipt' חייב להיות מחרוזת לא ריקה"
}
```

#### ❌ Development - Missing Specification
```
Status: 400 Bad Request

Response:
{
  "error": "'specification' חייב להיות מחרוזת עם לפחות 10 תווים"
}
```

#### ❌ Development - Short Specification
```
Status: 400 Bad Request

Response:
{
  "error": "'specification' חייב להיות לפחות 10 תווים, נמצאו 5"
}
```

#### ❌ Development - Invalid Branch Name
```
Status: 400 Bad Request

Response:
{
  "error": "'branchName' אינו שם תקין של branch: 'feature//invalid' - לא יכול להכיל //"
}
```

Or:
```
{
  "error": "'branchName' אינו שם תקין של branch: 'feature/test/' - לא יכול להסתיים ב-/"
}
```

Or:
```
{
  "error": "'branchName' אינו שם תקין של branch: 'feature test' - לא יכול להכיל רווחים"
}
```

#### ❌ Development - Invalid Version
```
Status: 400 Bad Request

Response:
{
  "error": "'versionNumber' חייב להיות בפורמט SemVer (major.minor.patch), קיבלנו: '1.2'"
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
  "error": "משתמש עם ID 999 לא קיים"
}
```

---

### Validation Errors (Create/Update)

#### ❌ Invalid Task Type
```
Status: 400 Bad Request

Response:
{
  "error": "TaskType חייב להיות מחרוזת לא ריקה"
}
```

#### ❌ Missing Description
```
Status: 400 Bad Request

Response:
{
  "error": "Description חייב להיות מחרוזת לא ריקה"
}
```

#### ❌ Unknown Task Type
```
Status: 400 Bad Request

Response:
{
  "error": "TaskType 'Unknown' לא רשום בהנדלרים"
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
  "newStatus": 99,
  "task": {
    "id": 1,
    "taskType": "Procurement",
    "description": "רכישת חומרים",
    "currentStatus": 99,
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
  "currentStatus": 0,
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
[
  {
    "id": 1,
    "taskType": "Procurement",
    "description": "רכישת חומרים",
    "currentStatus": 2,
    "assignedToUserId": 1,
    "customDataJson": "{\"prices\": [\"5000\", \"4800\"]}"
  },
  {
    "id": 2,
    "taskType": "Development",
    "description": "פיתוח API",
    "currentStatus": 1,
    "assignedToUserId": 1,
    "customDataJson": "{}"
  }
]
```

---

## Common Error Scenarios

### Scenario 1: Invalid Forward Jump

```
🔴 Request
POST /api/tasks/1/change-status
{ "newStatus": 3, "newDataJson": "{}" }

Current Status: 1

🔴 Response
Status: 400 Bad Request
{
  "error": "תנועה קדימה חייבת להיות בדיוק ב-1 סטטוס. סטטוס נוכחי: 1, מבוקש: 3"
}

✅ Solution: Move to status 2 first
POST /api/tasks/1/change-status
{ "newStatus": 2, "newDataJson": "{...}" }
```

### Scenario 2: Missing Handler Data

```
🔴 Request
POST /api/tasks/1/change-status
{ "newStatus": 2, "newDataJson": "{}" }

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
  "newDataJson": "{\"prices\": [\"5000\", \"4800\"]}"
}
```

### Scenario 3: Task Already Closed

```
🔴 Request
POST /api/tasks/1/change-status
{ "newStatus": 1, "newDataJson": "{}" }

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
  "newDataJson": "{not valid json}"
}

Current Task: Procurement, Status 1

🔴 Response
Status: 400 Bad Request
{
  "error": "JSON לא תקין ב-newDataJson"
}

✅ Solution: Use valid JSON
POST /api/tasks/1/change-status
{
  "newStatus": 2,
  "newDataJson": "{\"prices\": [\"5000\", \"4800\"]}"
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
    var result = await _service.ChangeStatusAsync(1, 3, "{}");

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

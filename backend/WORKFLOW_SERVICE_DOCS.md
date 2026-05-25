# 📋 TaskWorkflowService - Workflow Management

## 🎯 Overview

`TaskWorkflowService` מנהל את ה-Workflow הכללי של משימות עם כללים קפדניים לתנועה בין סטטוסים.

---

## 🏗️ Architecture

```
REST API (TasksController)
    ↓
ITaskApplicationService
    ↓
TaskApplicationService
    ├─ CRUD and read queries
    └─ delegates workflow operations
        ↓
ITaskWorkflowService
    ↓
TaskWorkflowService
    ├─ DbContext (EF Core)
    ├─ TaskHandlerFactory (Handlers)
    └─ JSON + workflow validation rules
```

---

## 📋 Workflow Rules

### 1. **בדיקה שהמשימה לא סגורה**
```csharp
// משימה סגורה (Status = 99) לא יכולה להשתנות
if (task.CurrentStatus == 99)  // ClosedStatus
    return Failure("משימה סגורה - לא ניתן לשנות סטטוס");
```

### 2. **בדיקת JSON לפני שינוי סטטוס**

`ChangeStatusAsync` דוחה `newDataJson` ריק או לא תקין לפני בדיקת תנועה או Handler:

```csharp
if (!IsValidJsonPayload(newDataJson))
    return Failure("NewDataJson חייב להיות JSON תקין");
```

### 3. **כללי תנועה בין סטטוסים**

#### תנועה קדימה (Forward): **בדיוק +1 סטטוס**
```
1 → 2 ✅
2 → 3 ✅
2 → 4 ❌ (דילוג, לא מותר)
```

#### תנועה אחורה (Backward): **לכל סטטוס נמוך יותר**
```
3 → 2 ✅ (rollback)
3 → 1 ✅ (rollback)
3 → 0 ✅ (rollback)
```

#### ללא תנועה: ❌
```
2 → 2 ❌ (אותו סטטוס)
```

### 4. **וולידציה ספציפית של Handler**
לאחר שהתנועה אושרה, Handler בודק וולידציה ספציפית:
```csharp
var handlerValidation = handler.ValidateStatusChange(
    currentDataJson,
    currentStatus,
    newStatus,
    newDataJson);
```

### 5. **בדיקת סטטוס סופי**
```csharp
// אי אפשר להעבור את סטטוס סופי של Handler
if (finalStatus.HasValue && currentStatus >= finalStatus && newStatus > currentStatus)
    return Failure($"משימה כבר הגיעה לסטטוס סופי ({finalStatus.Value})");
```

### 6. **עדכון נתונים**
```csharp
task.CurrentStatus = newStatus;
task.CustomDataJson = newDataJson;
task.UpdatedAt = DateTime.UtcNow;
```

---

## 📊 Example Workflows

### Procurement Task

```
Status 0: התחלה
   ↓ +1 (forward)
Status 1: בתהליך
   ↓ +1 (forward)
Status 2: בחירת ספקים ⭐
   Requires: {"prices": ["5000", "4800"]}
   ↓ +1 (forward)
Status 3: ✅ FinalStatus (לא יכול להעבור)
   Requires: {"receipt": "REC-123"}
   ↓
Status 99: Closed (סגור לנצח)

** אפשר להחזור:**
Status 2 → Status 1 ← (rollback)
Status 1 → Status 0 ← (rollback)
```

### Development Task

```
Status 0: התחלה
   ↓ +1
Status 1: בתהליך
   ↓ +1
Status 2: אפיון ⭐
   Requires: {"specification": "..."}
   ↓ +1
Status 3: בקידוד ⭐
   Requires: {"branchName": "feature/xyz"}
   ↓ +1
Status 4: ✅ FinalStatus
   Requires: {"versionNumber": "1.2.0"}
   ↓
Status 99: Closed (סגור לנצח)

** אפשר להחזור:**
Status 3 → Status 2 ← (rollback)
Status 2 → Status 1 ← (rollback)
Status 2 → Status 0 ← (rollback)
```

---

## 🔧 Services Implementation

### ITaskWorkflowService Methods

```csharp
public interface ITaskWorkflowService
{
    // שינוי סטטוס עם כללי workflow
    Task<WorkflowResult> ChangeStatusAsync(
        int taskId,
        int newStatus,
        string newDataJson,
        CancellationToken cancellationToken = default);
    
    // סגירת משימה
    Task<WorkflowResult> CloseTaskAsync(
        int taskId,
        string finalNotes,
        CancellationToken cancellationToken = default);
    
    // קבלת משימות של משתמש (לא סגורות)
    Task<IEnumerable<BaseTask>> GetUserTasksAsync(
        int userId,
        CancellationToken cancellationToken = default);
    
    // קבלת משימה עם פרטים
    Task<BaseTask?> GetTaskAsync(
        int taskId,
        CancellationToken cancellationToken = default);
}
```

Controllers pass `HttpContext.RequestAborted` through the application service layer so abandoned HTTP requests can cancel EF Core queries. Read methods use `AsNoTracking()` and include the assigned user; `GetUserTasksAsync` filters out closed tasks (`CurrentStatus != 99`).

### WorkflowResult

```csharp
public class WorkflowResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public int? NewStatus { get; set; }
    public BaseTask? UpdatedTask { get; set; }
}
```

---

## 🔌 REST API Endpoints

### 1. **Create Task**

```http
POST /api/tasks
Content-Type: application/json

{
  "taskType": "Procurement",
  "description": "רכישת רכיבים לשרת",
  "assignedToUserId": 1,
  "customDataJson": "{}"
}
```

**Response (201):**
```json
{
  "id": 1,
  "taskType": "Procurement",
  "description": "רכישת רכיבים לשרת",
  "currentStatus": 0,
  "assignedToUserId": 1,
  "customDataJson": "{}",
  "createdAt": "2026-05-25T10:00:00Z"
}
```

---

### 2. **Change Status with Workflow**

```http
POST /api/tasks/1/change-status
Content-Type: application/json

{
  "newStatus": 1,
  "newDataJson": "{}"
}
```

**Response (200) - Forward Movement:**
```json
{
  "success": true,
  "message": "סטטוס עודכן בהצלחה ל-1",
  "newStatus": 1,
  "task": { ... }
}
```

**Response (400) - Invalid Movement:**
```json
{
  "error": "תנועה קדימה חייבת להיות בדיוק ב-1 סטטוס. סטטוס נוכחי: 1, מבוקש: 3",
  "code": "workflow_validation_failed"
}
```

**Response (400) - Validation Failed:**
```json
{
  "error": "'prices' חייב להכיל בדיוק 2 מחרוזות, נמצאו 1",
  "code": "workflow_validation_failed"
}
```

Workflow failures are thrown as `WorkflowValidationException` by the controller and formatted by `GlobalExceptionMiddleware`.

---

### 3. **Close Task**

```http
POST /api/tasks/1/close
Content-Type: application/json

{
  "finalNotes": "משימה הושלמה בהצלחה"
}
```

**Response (200):**
```json
{
  "success": true,
  "message": "משימה סגורה בהצלחה",
  "task": {
    "id": 1,
    "currentStatus": 99,
    "customDataJson": "{\"finalNotes\": \"משימה הושלמה בהצלחה\", \"closedAt\": \"2026-05-25T...\"}"
  }
}
```

**Response (400) - Already Closed:**
```json
{
  "error": "משימה כבר סגורה"
}
```

---

### 4. **Get User Tasks**

```http
GET /api/tasks/user/1
```

**Response (200):**
```json
[
  {
    "id": 1,
    "taskType": "Procurement",
    "description": "רכישת רכיבים",
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

### 5. **Get Task**

```http
GET /api/tasks/1
```

**Response (200):**
```json
{
  "id": 1,
  "taskType": "Procurement",
  "description": "רכישת רכיבים",
  "currentStatus": 2,
  "assignedToUserId": 1,
  "customDataJson": "{\"prices\": [\"5000\", \"4800\"]}"
}
```

---

### 6. **Get All Tasks**

```http
GET /api/tasks
```

---

### 7. **Get Tasks by Type**

```http
GET /api/tasks/byType/Procurement
```

---

### 8. **Update Task**

```http
PUT /api/tasks/1
Content-Type: application/json

{
  "description": "תיאור חדש"
}
```

---

### 9. **Delete Task**

```http
DELETE /api/tasks/1
```

---

## 💡 Advanced Examples

### Example 1: Forward Movement (Procurement)

```json
// Status 1 → 2 (forward by +1)
POST /api/tasks/1/change-status

{
  "newStatus": 2,
  "newDataJson": "{\"prices\": [\"5000 ₪\", \"4800 ₪\"]}"
}

Response:
{
  "success": true,
  "message": "סטטוס עודכן בהצלחה ל-2"
}
```

### Example 2: Backward Movement (Rollback)

```json
// Status 2 → 1 (backward to lower status)
POST /api/tasks/1/change-status

{
  "newStatus": 1,
  "newDataJson": "{}"
}

Response:
{
  "success": true,
  "message": "סטטוס עודכן בהצלחה ל-1"
}
```

### Example 3: Invalid Forward Jump

```json
// Status 1 → 3 (invalid: more than +1)
POST /api/tasks/1/change-status

{
  "newStatus": 3,
  "newDataJson": "{}"
}

Response (400):
{
  "error": "תנועה קדימה חייבת להיות בדיוק ב-1 סטטוס..."
}
```

### Example 4: Close Task

```json
POST /api/tasks/1/close

{
  "finalNotes": "משימה הושלמה בהצלחה"
}

Response:
{
  "success": true,
  "message": "משימה סגורה בהצלחה",
  "task": {
    "id": 1,
    "currentStatus": 99,
    "customDataJson": "{\"finalNotes\": \"משימה הושלמה בהצלחה\", \"closedAt\": \"...\"}"
  }
}
```

---

## 🧪 Test Scenarios

### Scenario 1: Procurement Workflow

```bash
# 1. Create task (Status 0)
POST /api/tasks
{
  "taskType": "Procurement",
  "description": "רכישת חומרים",
  "assignedToUserId": 1
}
→ ID: 1, Status: 0 ✅

# 2. Move to Status 1
POST /api/tasks/1/change-status
{"newStatus": 1, "newDataJson": "{}"}
→ Status: 1 ✅

# 3. Move to Status 2 with prices
POST /api/tasks/1/change-status
{
  "newStatus": 2,
  "newDataJson": "{\"prices\": [\"5000\", \"4800\"]}"
}
→ Status: 2 ✅

# 4. Rollback to Status 1
POST /api/tasks/1/change-status
{"newStatus": 1, "newDataJson": "{}"}
→ Status: 1 ✅

# 5. Move forward again
POST /api/tasks/1/change-status
{
  "newStatus": 2,
  "newDataJson": "{\"prices\": [\"5500\", \"5200\"]}"
}
→ Status: 2 ✅

# 6. Move to Status 3 (Final)
POST /api/tasks/1/change-status
{
  "newStatus": 3,
  "newDataJson": "{\"prices\": [...], \"receipt\": \"REC-001\"}"
}
→ Status: 3 ✅ (FinalStatus)

# 7. Try to move beyond (should fail)
POST /api/tasks/1/change-status
{"newStatus": 4, "newDataJson": "{}"}
→ Error: "משימה כבר הגיעה לסטטוס סופי (3)" ❌

# 8. Close task
POST /api/tasks/1/close
{"finalNotes": "הושלם בהצלחה"}
→ Status: 99 ✅

# 9. Try to change closed task (should fail)
POST /api/tasks/1/change-status
{"newStatus": 2, "newDataJson": "{}"}
→ Error: "משימה סגורה" ❌
```

---

## 📋 Status Codes

| Code | Meaning | Example |
|------|---------|---------|
| 200 | ✅ Success | Status changed, task closed |
| 400 | ❌ Validation Error | Invalid movement, invalid JSON, validation failed, closed task |
| 404 | ❌ Not Found | Task doesn't exist |
| 500 | ❌ Server Error | Database error |

---

## 🎓 Key Concepts

1. **Forward Movement**: Must be exactly +1 status
2. **Backward Movement**: Allowed to any lower status (rollback)
3. **Closed Status**: 99 (permanent, cannot be changed)
4. **Final Status**: Handler-specific status that cannot be exceeded
5. **JSON Payload**: `newDataJson` must be valid JSON before workflow validation
6. **Workflow Validation**: Combines movement rules + handler validation

---

**TaskWorkflowService מוכן! 🚀**

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
TaskWorkflowService
    ├─ DbContext (EF Core)
    └─ ITaskWorkflowRuleProvider[] (ordered by Priority)
        ├─ MetadataTaskWorkflowRuleProvider (Priority 0)
        │   └─ ITaskTypeValidationService / task type metadata
        └─ HandlerTaskWorkflowRuleProvider (Priority 100)
            └─ TaskHandlerFactory / ITaskHandler fallback
```

`TaskWorkflowService` owns generic movement rules and persistence. It delegates task-type-specific final-status and payload validation to the first rule provider that can handle the task type.

### Rule provider resolution

1. Providers are registered in DI as `ITaskWorkflowRuleProvider` in `Program.cs`.
2. The service sorts providers by `Priority` ascending.
3. The first provider where `CanHandle(taskType)` returns `true` validates the transition.
4. Built-in precedence:
   - Metadata-backed task types win (`MetadataTaskWorkflowRuleProvider`, priority `0`).
   - Handler-backed task types are fallback (`HandlerTaskWorkflowRuleProvider`, priority `100`).

This keeps workflow extensible without adding task-type branches to `TaskWorkflowService`.

---

## 📋 Workflow Rules

### 1. **בדיקה שהמשימה לא סגורה**
```csharp
// משימה סגורה (Status = 99) לא יכולה להשתנות
if (task.CurrentStatus == 99)  // ClosedStatus
    return Failure("משימה סגורה - לא ניתן לשנות סטטוס");
```

### 2. **כללי תנועה בין סטטוסים**

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
3 → 0 ❌ (backend statuses must be >= CreatedStatus, currently 1)
```

#### ללא תנועה: ❌
```
2 → 2 ❌ (אותו סטטוס)
```

### 3. **וולידציה ספציפית של Rule Provider**
לאחר שהתנועה אושרה, ספק הכללים שנבחר בודק וולידציה ספציפית:
```csharp
var validationResult = ruleProvider.ValidateStatusChange(
    task,
    nextStatus,
    newDataJson);
```

אם סוג המשימה מוגדר במטאדאטה, הוולידציה מגיעה מ-`TaskTypeValidationService`. אם אין מטאדאטה אבל קיים `ITaskHandler`, ה-handler הוא fallback.

### 4. **בדיקת סטטוס סופי**
```csharp
// אי אפשר לעבור את הסטטוס הסופי שה-rule provider מחזיר
if (finalStatus.HasValue && currentStatus >= finalStatus && newStatus > currentStatus)
    return Failure("משימה הגיעה לסטטוס סופי");
```

### 5. **עדכון נתונים**
```csharp
task.CurrentStatus = newStatus;
task.CustomDataJson = newDataJson;
task.UpdatedAt = DateTime.UtcNow;
```

---

## 📊 Example Workflows

### Procurement Task

```
Status 1: Created / בתהליך
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
```

### Development Task

```
Status 1: Created / בתהליך
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
        int nextAssignedToUserId,
        string newDataJson,
        CancellationToken cancellationToken = default);
    
    // סגירת משימה
    Task<WorkflowResult> CloseTaskAsync(
        int taskId,
        string finalNotes,
        CancellationToken cancellationToken = default);

    // בדיקה האם מותר לשנות משימה שאינה שינוי סטטוס
    Task<WorkflowResult> EnsureTaskMutableAsync(
        int taskId,
        CancellationToken cancellationToken = default);
    
    // קבלת משימות של משתמש
    Task<IEnumerable<BaseTask>> GetUserTasksAsync(
        int userId,
        CancellationToken cancellationToken = default);
    
    // קבלת משימה עם פרטים
    Task<BaseTask?> GetTaskAsync(
        int taskId,
        CancellationToken cancellationToken = default);
}
```

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
  "customFields": {}
}
```

**Response (201):**
```json
{
  "id": 1,
  "taskType": "Procurement",
  "description": "רכישת רכיבים לשרת",
  "currentStatus": 1,
  "assignedToUserId": 1,
  "customFields": {},
  "createdAt": "2026-05-25T10:00:00Z"
}
```

---

### 2. **Change Status with Workflow**

```http
POST /api/tasks/1/change-status
Content-Type: application/json

{
  "newStatus": 2,
  "nextAssignedToUserId": 1,
  "customFields": {
    "prices": ["5000", "4800"]
  }
}
```

**Response (200) - Forward Movement:**
```json
{
  "success": true,
  "message": "סטטוס עודכן בהצלחה ל-2",
  "newStatus": 2,
  "task": { ... }
}
```

**Response (400) - Invalid Movement:**
```json
{
  "error": "תנועה קדימה חייבת להיות בדיוק ב-1 סטטוס. סטטוס נוכחי: 1, מבוקש: 3"
}
```

**Response (400) - Validation Failed:**
```json
{
  "error": "'prices' חייב להכיל בדיוק 2 מחרוזות, נמצאו 1"
}
```

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
    "customFields": {
      "finalNotes": "משימה הושלמה בהצלחה",
      "closedAt": "2026-05-25T..."
    }
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
{
  "items": [
    {
      "id": 1,
      "taskType": "Procurement",
      "description": "רכישת רכיבים",
      "currentStatus": 2,
      "assignedToUserId": 1,
      "createdAt": "2026-05-25T10:00:00Z",
      "updatedAt": "2026-05-25T10:10:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

List endpoints return `TaskSummaryDto` items and do not include `customFields`; call `GET /api/tasks/{id}` for details.

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
  "customFields": {
    "prices": ["5000", "4800"]
  }
}
```

---

### 6. **Get All Tasks**

```http
GET /api/tasks?page=1&pageSize=20
```

---

### 7. **Get Tasks by Type**

```http
GET /api/tasks/byType/Procurement?page=1&pageSize=20
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
  "nextAssignedToUserId": 1,
  "customFields": {
    "prices": ["5000 ₪", "4800 ₪"]
  }
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
  "nextAssignedToUserId": 1,
  "customFields": {}
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
  "nextAssignedToUserId": 1,
  "customFields": {}
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
    "customFields": {
      "finalNotes": "משימה הושלמה בהצלחה",
      "closedAt": "..."
    }
  }
}
```

---

## 🧪 Test Scenarios

### Scenario 1: Procurement Workflow

```bash
# 1. Create task (Status 1)
POST /api/tasks
{
  "taskType": "Procurement",
  "description": "רכישת חומרים",
  "assignedToUserId": 1
}
→ Status: 1 ✅

# 2. Move to Status 2 with prices
POST /api/tasks/1/change-status
{
  "newStatus": 2,
  "nextAssignedToUserId": 1,
  "customFields": { "prices": ["5000", "4800"] }
}
→ Status: 2 ✅

# 3. Rollback to Status 1
POST /api/tasks/1/change-status
{"newStatus": 1, "nextAssignedToUserId": 1, "customFields": {}}
→ Status: 1 ✅

# 4. Move forward again
POST /api/tasks/1/change-status
{
  "newStatus": 2,
  "nextAssignedToUserId": 1,
  "customFields": { "prices": ["5500", "5200"] }
}
→ Status: 2 ✅

# 5. Move to Status 3 (Final)
POST /api/tasks/1/change-status
{
  "newStatus": 3,
  "nextAssignedToUserId": 1,
  "customFields": { "receipt": "REC-001" }
}
→ Status: 3 ✅ (FinalStatus)

# 6. Try to move beyond (should fail)
POST /api/tasks/1/change-status
{"newStatus": 4, "nextAssignedToUserId": 1, "customFields": {}}
→ Error: "משימה הגיעה לסטטוס סופי" ❌

# 7. Close task
POST /api/tasks/1/close
{"finalNotes": "הושלם בהצלחה"}
→ Status: 99 ✅

# 8. Try to change closed task (should fail)
POST /api/tasks/1/change-status
{"newStatus": 2, "nextAssignedToUserId": 1, "customFields": {}}
→ Error: "משימה סגורה" ❌
```

---

## 📋 Status Codes

| Code | Meaning | Example |
|------|---------|---------|
| 200 | ✅ Success | Status changed, task closed |
| 400 | ❌ Validation Error | Invalid movement, validation failed, closed task |
| 404 | ❌ Not Found | Task doesn't exist |
| 500 | ❌ Server Error | Database error |

---

## 🎓 Key Concepts

1. **Forward Movement**: Must be exactly +1 status
2. **Backward Movement**: Allowed to any lower status at or above `WorkflowConstants.CreatedStatus`
3. **Closed Status**: 99 (permanent, cannot be changed)
4. **Final Status**: Provider-specific status that cannot be exceeded
5. **Workflow Validation**: Combines movement rules + selected rule-provider validation

---

**TaskWorkflowService מוכן! 🚀**

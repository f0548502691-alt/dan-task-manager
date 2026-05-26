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
    ├─ MetadataTaskWorkflowRuleProvider (priority 0)
    ├─ HandlerTaskWorkflowRuleProvider (priority 100)
    └─ Workflow movement + assignment invariants
```

`TaskWorkflowService` owns the generic workflow invariants. Task-type field validation comes from
`TaskTypeValidationService` metadata when a task type exists in the database; handler validation is
the fallback for handler-only task types.

---

## 📋 Workflow Rules

### 1. **בדיקה שהמשימה לא סגורה**
```csharp
// משימה סגורה (Status = 99) לא יכולה להשתנות
if (task.CurrentStatus == 99)  // ClosedStatus
    return Failure("משימה סגורה - לא ניתן לשנות סטטוס");
```

### 2. **כללי תנועה בין סטטוסים**

Status `1` is the first real workflow status (`WorkflowConstants.CreatedStatus`). Status `0` is not a
valid transition target and should not be exposed by clients.

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
3 → 0 ❌ (status 0 is below CreatedStatus)
```

#### ללא תנועה: ❌
```
2 → 2 ❌ (אותו סטטוס)
```

### 3. **Assignment and JSON payload are required**
Every status change and close request must name the next assignee. The user must exist, and the task is
persisted with `AssignedToUserId = nextAssignedToUserId` when the workflow operation succeeds.

`customFields` must deserialize to a JSON object. Arrays, strings, numbers, empty payloads, and invalid
JSON are rejected before task-type validation runs.

```csharp
Task<WorkflowResult> ChangeStatusAsync(
    int taskId,
    int newStatus,
    int nextAssignedToUserId,
    string newDataJson,
    CancellationToken cancellationToken = default);
```

### 4. **Task-type validation**
לאחר שהתנועה אושרה, ספק הכללים המתאים בודק את הוולידציה הספציפית:
```csharp
var ruleProvider = ResolveRuleProvider(task.TaskType);
var validationResult = ruleProvider.ValidateStatusChange(task, newStatus, newDataJson);
```

Provider order:
- `MetadataTaskWorkflowRuleProvider` (priority `0`) uses database/config metadata from `TaskTypeValidationService`.
- `HandlerTaskWorkflowRuleProvider` (priority `100`) falls back to `ITaskHandler` implementations.

### 5. **בדיקת סטטוס סופי**
```csharp
// אי אפשר להעבור את סטטוס סופי של Handler
if (finalStatus.HasValue && currentStatus >= finalStatus && newStatus > currentStatus)
    return Failure("משימה הגיעה לסטטוס סופי");
```

Closing is separate from movement. Clients must call `POST /api/tasks/{id}/close` from the task type's
final status; attempting to set status `99` through `change-status` fails.

### 6. **עדכון נתונים**
```csharp
task.CurrentStatus = newStatus;
task.AssignedToUserId = nextAssignedToUserId;
task.CustomDataJson = newDataJson;
task.UpdatedAt = DateTime.UtcNow;
```

---

## 📊 Example Workflows

### Procurement Task

```
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
```

### Development Task

```
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
        int nextAssignedToUserId,
        string finalNotes,
        CancellationToken cancellationToken = default);
    
    // בדיקה האם מותר לבצע שינוי שאינו שינוי סטטוס
    Task<WorkflowResult> EnsureTaskMutableAsync(
        int taskId,
        CancellationToken cancellationToken = default);

    // קבלת משימות של משתמש
    Task<IEnumerable<BaseTask>> GetUserTasksAsync(int userId);
    
    // קבלת משימה עם פרטים
    Task<BaseTask?> GetTaskAsync(int taskId);
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
  "nextAssignedToUserId": 1,
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
    "assignedToUserId": 1,
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
{
  "items": [
    {
      "id": 1,
      "taskType": "Procurement",
      "description": "רכישת רכיבים",
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

List endpoints return `TaskSummaryDto` items, so `customDataJson` is intentionally omitted. Use
`GET /api/tasks/{id}` when the custom payload is needed.

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
  "nextAssignedToUserId": 1,
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
  "customFields": {"prices": ["5000", "4800"]}
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
  "customFields": {"prices": ["5500", "5200"]}
}
→ Status: 2 ✅

# 5. Move to Status 3 (Final)
POST /api/tasks/1/change-status
{
  "newStatus": 3,
  "nextAssignedToUserId": 1,
  "customFields": {"receipt": "REC-001"}
}
→ Status: 3 ✅ (FinalStatus)

# 6. Try to move beyond (should fail)
POST /api/tasks/1/change-status
{"newStatus": 4, "nextAssignedToUserId": 1, "customFields": {}}
→ Error: "משימה הגיעה לסטטוס סופי" ❌

# 7. Close task
POST /api/tasks/1/close
{"nextAssignedToUserId": 1, "finalNotes": "הושלם בהצלחה"}
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
2. **Backward Movement**: Allowed to any lower status (rollback)
3. **Closed Status**: 99 (permanent, cannot be changed)
4. **Created Status**: 1 (status 0 is not accepted)
5. **Assignment**: `nextAssignedToUserId` is required for change-status and close
6. **Final Status**: Metadata/handler-specific status that cannot be exceeded
7. **Workflow Validation**: Combines movement rules + metadata/handler validation

---

**TaskWorkflowService מוכן! 🚀**

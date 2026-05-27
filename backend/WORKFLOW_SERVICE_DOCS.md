# 📋 TaskWorkflowService - Workflow Management

## 🎯 Overview

`TaskWorkflowService` מנהל את ה-Workflow הכללי של משימות עם כללים קפדניים לתנועה בין סטטוסים.

---

## 🏗️ Architecture

```
REST API (TasksController)
    ├─ POST /api/tasks
    │   └─ IMediator.Send(CreateTaskCommand)
    │       └─ CreateTaskCommandHandler
    │           └─ ITaskApplicationService.CreateAsync(...)
    │               ├─ DbContext (EF Core)
    │               ├─ TaskHandlerFactory (handler-backed task types)
    │               └─ ITaskTypeValidationService (metadata-backed task types)
    │
    └─ reads, status changes, close, update, delete
        └─ ITaskApplicationService
            └─ ITaskWorkflowService / TaskWorkflowService
                ├─ DbContext (EF Core)
                ├─ ITaskWorkflowRuleProvider implementations
                └─ Validation Rules
```

### Create-task command path

`POST /api/tasks` is the first endpoint in the gradual MediatR migration.
The controller still owns HTTP request validation and response shaping, but it
now sends a `CreateTaskCommand` through `IMediator`.

Important constraints:

- `CreateTaskCommandHandler` is intentionally thin: it adapts the MediatR
  command to `Services.TaskCreateCommand` and delegates to
  `ITaskApplicationService.CreateAsync`.
- `TaskApplicationService.CreateAsync` remains the source of create-task
  invariants: assigned user must exist, `customFields` must normalize to a JSON
  object, the task type must be supported by a handler or metadata definition,
  and new tasks start at `WorkflowConstants.CreatedStatus` (`1`).
- Public API payloads use `customFields`; the persistence model stores the same
  object as `BaseTask.CustomDataJson`.
- Other endpoints have not moved to MediatR yet. Do not assume status changes,
  reads, close, update, or delete requests go through commands/queries until
  their controllers are migrated.
- MediatR handlers are registered from the backend assembly in `Program.cs`:
  `builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));`

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
```

#### ללא תנועה: ❌
```
2 → 2 ❌ (אותו סטטוס)
```

### 3. **וולידציה ספציפית של Handler**
לאחר שהתנועה אושרה, Handler בודק וולידציה ספציפית:
```csharp
var handlerValidation = handler.ValidateStatusChange(
    currentDataJson,
    currentStatus,
    newStatus,
    newDataJson);
```

### 4. **בדיקת סטטוס סופי**
```csharp
// אי אפשר להעבור את סטטוס סופי של Handler
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
Status 1: נוצרה
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
Status 1: נוצרה
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
    
    // בדיקה האם מותר לבצע שינוי במשימה שאינה שינוי סטטוס
    Task<WorkflowResult> EnsureTaskMutableAsync(
        int taskId,
        CancellationToken cancellationToken = default);

    // קבלת משימות של משתמש מסוים
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
  "customFields": {
    "priority": "high"
  }
}
```

Request notes:

- `customFields` is optional; when it is missing, the service stores `{}`.
- If provided, `customFields` must be a JSON object. Arrays, strings, and other
  scalar values are rejected.
- The controller serializes `customFields` into the command's
  `CustomDataJson` value before dispatching `CreateTaskCommand`.

**Response (201):**
```json
{
  "id": 1,
  "taskType": "Procurement",
  "description": "רכישת רכיבים לשרת",
  "currentStatus": 1,
  "assignedToUserId": 1,
  "createdAt": "2026-05-25T10:00:00Z",
  "updatedAt": "2026-05-25T10:00:00Z",
  "assignedToUser": {
    "id": 1,
    "name": "דן כהן",
    "email": "dan@example.com"
  },
  "customFields": {
    "priority": "high"
  }
}
```

**Response (400) - Unsupported task type:**
```json
{
  "error": "סוג משימה לא נתמך: Unknown",
  "supportedTaskTypes": [
    "Analysis",
    "Development",
    "Procurement",
    "Testing"
  ]
}
```

---

### 2. **Change Status with Workflow**

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
      "prices": ["5000", "4800"],
      "receipt": "REC-001",
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
      "updatedAt": "2026-05-25T10:30:00Z",
      "assignedToUser": {
        "id": 1,
        "name": "דן כהן",
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
  "createdAt": "2026-05-25T10:00:00Z",
  "updatedAt": "2026-05-25T10:30:00Z",
  "assignedToUser": {
    "id": 1,
    "name": "דן כהן",
    "email": "dan@example.com"
  },
  "customFields": {
    "prices": ["5000", "4800"]
  }
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
  "nextAssignedToUserId": 2,
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
  "nextAssignedToUserId": 2,
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
  "assignedToUserId": 1,
  "customFields": {}
}
→ ID: 1, Status: 1 ✅

# 2. Move to Status 2 with prices
POST /api/tasks/1/change-status
{
  "newStatus": 2,
  "nextAssignedToUserId": 2,
  "customFields": {
    "prices": ["5000", "4800"]
  }
}
→ Status: 2 ✅

# 3. Rollback to Status 1
POST /api/tasks/1/change-status
{
  "newStatus": 1,
  "nextAssignedToUserId": 1,
  "customFields": {}
}
→ Status: 1 ✅

# 4. Move forward again
POST /api/tasks/1/change-status
{
  "newStatus": 2,
  "nextAssignedToUserId": 2,
  "customFields": {
    "prices": ["5500", "5200"]
  }
}
→ Status: 2 ✅

# 5. Move to Status 3 (Final)
POST /api/tasks/1/change-status
{
  "newStatus": 3,
  "nextAssignedToUserId": 2,
  "customFields": {
    "prices": ["5500", "5200"],
    "receipt": "REC-001"
  }
}
→ Status: 3 ✅ (FinalStatus)

# 6. Try to move beyond (should fail)
POST /api/tasks/1/change-status
{
  "newStatus": 4,
  "nextAssignedToUserId": 2,
  "customFields": {}
}
→ Error: "משימה הגיעה לסטטוס סופי" ❌

# 7. Close task
POST /api/tasks/1/close
{"finalNotes": "הושלם בהצלחה"}
→ Status: 99 ✅

# 8. Try to change closed task (should fail)
POST /api/tasks/1/change-status
{
  "newStatus": 2,
  "nextAssignedToUserId": 2,
  "customFields": {}
}
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
4. **Final Status**: Handler-specific status that cannot be exceeded
5. **Workflow Validation**: Combines movement rules + handler validation

---

**TaskWorkflowService מוכן! 🚀**

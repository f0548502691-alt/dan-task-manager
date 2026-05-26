# 🎯 Strategy Pattern & Task Handlers - תיעוד

## 📋 מבט כללי

הפרויקט מנצל את **Strategy Pattern** עם **Factory** כדי לאפשר הרחבה של סוגי משימות חדשים ללא שינוי קוד קיים (**Open/Closed Principle**).

### Current implementation snapshot

- Source code: `Domain/Handlers/*TaskHandler.cs`, `Services/TaskHandlerRegistrationExtensions.cs`, `Services/TaskApplicationService.cs`.
- Registered task types are discovered automatically from public, non-abstract `ITaskHandler` implementations in namespace `DanTaskManager.Domain.Handlers`.
- Current handlers: `Analysis`, `Development`, `Procurement`, `Testing`.
- New tasks start at `WorkflowConstants.CreatedStatus` (`1`); closed tasks use `WorkflowConstants.ClosedStatus` (`99`).
- Creating a task with an unknown `taskType` fails with HTTP 400 and includes `supportedTaskTypes` when the service can report the registered handlers.

---

## 🏗️ ארכיטקטורה

```
┌─────────────────────────────────────────────────────────┐
│                    BaseTask                             │
│  (TaskType, CurrentStatus, CustomDataJson, וכו')      │
└────────┬────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────┐
│          ITaskApplicationService                        │
│  - CreateAsync()                                       │
│  - ChangeStatusAsync()                                 │
└────────┬────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────┐
│           TaskHandlerFactory                            │
│  - GetHandler(taskType)                                │
│  - HasHandler(taskType)                                │
│  - GetRegisteredTaskTypes()                            │
└────────┬────────────────────────────────────────────────┘
         │
         ▼
    ┌────────────────────┐
    │  ITaskHandler      │◄────┐
    └────────────────────┘     │
         ▲                      │
         │      יורשים         │
         ├──────────────────────┘
         │
    ┌────┴───────┬──────────────────────┬──────────────────────┐
    │            │                      │                      │
    ▼            ▼                      ▼                      ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ Analysis        │ │ Development     │ │ Procurement     │ │ Testing         │
│ FinalStatus: 2  │ │ FinalStatus: 4  │ │ FinalStatus: 3  │ │ FinalStatus: 3  │
└─────────────────┘ └─────────────────┘ └─────────────────┘ └─────────────────┘
```

---

## 🔧 מחלקות ומימשקים

### 1. **ITaskHandler** - ממשק Strategy

```csharp
public interface ITaskHandler
{
    string TaskType { get; }           // שם סוג המשימה
    int FinalStatus { get; }           // הסטטוס הסופי (שלא ניתן להעבור אותו)
    
    ValidationResult ValidateStatusChange(
        string currentDataJson,
        int currentStatus,
        int nextStatus,
        string newDataJson);
}
```

**מה שהממשק קובע:**
- כל handler חייב להגדיר את `TaskType` (שם ייחודי)
- כל handler חייב להגדיר `FinalStatus` (סטטוס סופי)
- כל handler חייב לממש וולידציה לשינוי סטטוס

---

### 2. **Registered handlers**

| TaskType | FinalStatus | Required data by status |
|----------|-------------|-------------------------|
| `Analysis` | 2 | Status 2: `{"analysisReport": "..."}` with a non-empty string |
| `Procurement` | 3 | Status 2: `prices` array with exactly 2 non-empty strings; Status 3: non-empty `receipt` string |
| `Development` | 4 | Status 2: `specification` string of at least 10 characters; Status 3: valid `branchName`; Status 4: non-empty `versionNumber` string or number |
| `Testing` | 3 | Status 2: numeric `testCases` greater than 0; Status 3: `coverage` percent string from 0% to 100% plus non-empty `summary` |

---

### 3. **ProcurementTaskHandler**

**סטטוס סופי:** 3

| סטטוס | דרישה | דוגמה JSON |
|-------|-------|-----------|
| 1 | - | - |
| 2 | מערך של **2 מחרוזות** (מחירים) | `{"prices": ["5000 ₪", "4800 ₪"]}` |
| 3 | **מחרוזת** קבלה | `{"prices": [...], "receipt": "REC-123"}` |

**וולידציה:**
- בסטטוס 2: בדיקה שקיים שדה `prices` עם בדיוק 2 מחרוזות
- בסטטוס 3: בדיקה שקיים שדה `receipt` עם מחרוזת לא ריקה

---

### 4. **DevelopmentTaskHandler**

**סטטוס סופי:** 4

| סטטוס | דרישה | דוגמה JSON |
|-------|-------|-----------|
| 1 | - | - |
| 2 | **טקסט אפיון** (min 10 תווים) | `{"specification": "יש לפתח..."}` |
| 3 | **שם בראנץ'** תקין | `{"specification": "...", "branchName": "feature/xyz"}` |
| 4 | **מספר גרסה** | `{"versionNumber": "1.2.0"}` |

**וולידציה:**
- בסטטוס 2: בדיקה שדה `specification` עם לפחות 10 תווים
- בסטטוס 3: בדיקה שדה `branchName` תקין (ללא `//`, סיומת `/` או `.`, ורווחים)
- בסטטוס 4: בדיקה שדה `versionNumber` כמחרוזת או מספר לא ריקים; אם קיימות נקודות, כל חלק צריך להיות מספר

---

### 5. **TaskHandlerFactory**

```csharp
public class TaskHandlerFactory
{
    public TaskHandlerFactory(IEnumerable<ITaskHandler> handlers);
    
    public ITaskHandler? GetHandler(string taskType);
    public bool HasHandler(string taskType);
    public IEnumerable<string> GetRegisteredTaskTypes();
}
```

**עבודה:**
- בונה מפה של `TaskType` → `ITaskHandler`
- מחזירה את ה-Handler המתאים לפי סוג משימה
- מעריכה case-insensitive (לא משנה רישיות)
- `TaskType` חייב להיות ייחודי ללא תלות ברישיות; כפילות תגרום לכשל בבניית המפה
- `GetRegisteredTaskTypes()` משמש את יצירת המשימה כדי להחזיר הצעות ללקוח כאשר `taskType` לא נתמך

---

### 6. **ITaskApplicationService / ITaskWorkflowService**

```csharp
public interface ITaskApplicationService
{
    Task<TaskCreationResult> CreateAsync(TaskCreateCommand command, CancellationToken cancellationToken = default);
    Task<WorkflowResult> ChangeStatusAsync(
        int taskId,
        int newStatus,
        int nextAssignedToUserId,
        string newDataJson,
        CancellationToken cancellationToken = default);
}
```

**עבודה:**
1. `TaskApplicationService.CreateAsync` בודק שהמשתמש קיים, שה-JSON תקין, ושיש Handler רשום ל-`taskType`.
2. אם `taskType` לא נתמך, הוא מחזיר `TaskCreationResult.FailureResult` עם `SupportedTaskTypes`.
3. `TaskWorkflowService.ChangeStatusAsync` מוצא את ה-Handler לפי `task.TaskType`, בודק תנועה בין סטטוסים, וקורא ל-`ValidateStatusChange`.
4. ה-Controller ממיר כשלי create ל-400, ומוסיף `supportedTaskTypes` רק כאשר הרשימה אינה ריקה.

---

## 🔑 עקרונות SOLID שמומשו

### 1. **Open/Closed Principle** ✅

```
פתוח להרחבה:
  - אפשר להוסיף Handler חדש (TestingTaskHandler) 
    בלי לשנות קוד קיים

סגור לשינוי:
  - TaskHandlerFactory לא משתנה
  - ITaskWorkflowService לא משתנה
  - BaseTask לא משתנה
```

**דוגמה - הוספת Handler חדש:**
```csharp
// 1. יצירת Handler חדש
public class TestingTaskHandler : ITaskHandler
{
    public string TaskType => "Testing";
    public int FinalStatus => 2;
    public ValidationResult ValidateStatusChange(...) { ... }
}

// 2. שמירה תחת namespace DanTaskManager.Domain.Handlers
// Program.cs כבר קורא ל-AddTaskHandlersFromAssembly(typeof(ITaskHandler).Assembly)

// 3. זהו! TaskHandlerFactory יקבל אותו אוטומטית
```

---

### 2. **Single Responsibility Principle** ✅

- **ITaskHandler**: אחראי רק לוולידציה ספציפית של סוג משימה
- **TaskHandlerFactory**: אחראי רק ליצור את ה-Handler הנכון
- **TaskWorkflowService**: אחראי רק לתזמור שינויי סטטוס
- **TaskApplicationService**: אחראי על חוזה ה-API הגבוה, כולל כשלי יצירה עם סוגים נתמכים

---

### 3. **Dependency Inversion Principle** ✅

- התוכנה תלויה בממשקים (`ITaskHandler`, `ITaskWorkflowService`, `ITaskApplicationService`)
- לא בממשקים (`ProcurementTaskHandler`, `DevelopmentTaskHandler`)

---

## 💾 Dependency Injection - Program.cs

```csharp
// הרשמה אוטומטית של כל ה-Handlers מתוך DanTaskManager.Domain.Handlers
builder.Services.AddTaskHandlersFromAssembly(typeof(ITaskHandler).Assembly);

// הרשמה של Factory (מוזרקים אליו כל ה-Handlers שנמצאו)
builder.Services.AddScoped<TaskHandlerFactory>();

// הרשמה של Service
builder.Services.AddScoped<ITaskWorkflowService, TaskWorkflowService>();
builder.Services.AddScoped<ITaskApplicationService, TaskApplicationService>();
```

`AddTaskHandlersFromAssembly` רושם רק מחלקות public/non-abstract שמממשות `ITaskHandler` ונמצאות ב-namespace המדויק `DanTaskManager.Domain.Handlers`.

---

## 📊 REST API Endpoints

### שינוי סטטוס עם וולידציה

```http
POST /api/tasks/{id}/change-status
Content-Type: application/json

{
  "newStatus": 2,
  "nextAssignedToUserId": 3,
  "newDataJson": "{\"prices\": [\"5000 ₪\", \"4800 ₪\"]}"
}
```

**תוצאה בהצלחה (200):**
```json
{
  "success": true,
  "message": "סטטוס עודכן בהצלחה מ-1 ל-2",
  "task": { ... }
}
```

**תוצאה בכישלון (400):**
```json
{
  "error": "'prices' חייב להכיל בדיוק 2 מחרוזות, נמצאו 1"
}
```

---

## 🧪 בדיקה יחידתית (Unit Tests) - דוגמה

```csharp
[Fact]
public void ProcurementHandler_ValidateStatus2_WithTwoPrices_ShouldPass()
{
    // Arrange
    var handler = new ProcurementTaskHandler();
    var json = JsonSerializer.Serialize(new { prices = new[] { "5000", "4800" } });
    
    // Act
    var result = handler.ValidateStatusChange("{}", 1, 2, json);
    
    // Assert
    Assert.True(result.IsValid);
}

[Fact]
public void ProcurementHandler_ValidateStatus2_WithOnlyOnePrice_ShouldFail()
{
    // Arrange
    var handler = new ProcurementTaskHandler();
    var json = JsonSerializer.Serialize(new { prices = new[] { "5000" } });
    
    // Act
    var result = handler.ValidateStatusChange("{}", 1, 2, json);
    
    // Assert
    Assert.False(result.IsValid);
}
```

---

## 📂 מבנה קבצים

```
Domain/
├── BaseTask.cs
├── AppUser.cs
└── Handlers/
    ├── ITaskHandler.cs                  // ממשק
    ├── StatusValidationTaskHandlerBase.cs // בסיס משותף לוולידציה לפי סטטוס
    ├── AnalysisTaskHandler.cs           // Implementation
    ├── ProcurementTaskHandler.cs       // Implementation
    ├── DevelopmentTaskHandler.cs       // Implementation
    ├── TestingTaskHandler.cs           // Implementation
    └── TaskHandlerFactory.cs           // Factory

Services/
├── TaskHandlerRegistrationExtensions.cs // auto-discovery ל-Handlers
├── ITaskWorkflowService.cs             // ממשק workflow
├── TaskWorkflowService.cs              // Implementation
├── ITaskApplicationService.cs          // חוזה API פנימי
└── TaskApplicationService.cs           // orchestration ל-Controller

Controllers/
├── TasksController.cs                  // create/change-status/close/list
└── UsersController.cs
```

---

## 📖 דוגמאות שימוש

ראה [STRATEGY_EXAMPLES.cs](STRATEGY_EXAMPLES.cs) לדוגמאות קוד מלאות של:
1. שימוש ישיר ב-Handlers
2. Procurement flow
3. Development flow
4. TaskWorkflowService
5. Factory pattern
6. API endpoints

---

## 🚀 איך להרחיב? (5 דקות)

1. **יצור מחלקה חדשה עבור TestingTaskHandler**
   ```csharp
   public class TestingTaskHandler : ITaskHandler
   ```

2. **הטמע את ITaskHandler**
   - `TaskType` (לדוגמה: "Testing")
   - `FinalStatus` (לדוגמה: 2)
   - `ValidateStatusChange()` עם לוגיקה ספציפית

3. **ודא שהמחלקה מתגלה אוטומטית**
   - המחלקה צריכה להיות public ולא abstract
   - היא צריכה לממש `ITaskHandler`
   - היא צריכה להיות תחת namespace `DanTaskManager.Domain.Handlers`
   - אין להוסיף שורת DI ידנית כאשר משתמשים ב-`AddTaskHandlersFromAssembly`

4. **סיום!** TaskHandlerFactory ו-TaskWorkflowService יעבדו אוטומטית

---

## 🎓 משהו לדעת

- **CustomDataJson**: שדה JSON גמיש לנתונים המשתנים לפי סוג משימה
- **FinalStatus**: סטטוס סופי - משימה לא יכולה להתקדם מעבר לו
- **Validation**: וולידציה מתבצעת בסטטוס מסוים, לא בכולם
- **Case-insensitive**: TaskType מכופה case-insensitive
- **Unsupported type response**: Create-task failures for unknown `taskType` return `supportedTaskTypes` sorted case-insensitively

---

**מעולה! 🎉**

# 🎯 Strategy Pattern & Task Handlers - תיעוד

## 📋 מבט כללי

הפרויקט מנצל את **Strategy Pattern** עם **Factory** כדי לאפשר הרחבה של סוגי משימות חדשים ללא שינוי קוד קיים (**Open/Closed Principle**). ה-API עובר דרך שכבת Application Services, ו-`TaskWorkflowService` משתמש ב-Factory כדי להפעיל את ה-Handler המתאים.

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
│          TaskWorkflowService                            │
│  - ChangeStatusAsync()                                 │
│  - CloseTaskAsync()                                    │
│  - JSON + movement validation                          │
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
         │      יורשים דרך StatusValidationTaskHandlerBase
         ├──────────────────────┘
         │
    ┌────┴───────┬──────────────────────┐
    │            │                      │
    ▼            ▼                      ▼
┌─────────────────┐    ┌──────────────────────────────┐
│  Procurement    │    │  Development                 │
│  TaskHandler    │    │  TaskHandler                 │
│                 │    │                              │
│ FinalStatus: 3  │    │ FinalStatus: 4               │
└─────────────────┘    └──────────────────────────────┘
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

### 2. **StatusValidationTaskHandlerBase**

`ProcurementTaskHandler` ו-`DevelopmentTaskHandler` יורשים מ-`StatusValidationTaskHandlerBase` במקום לשכפל `if` לכל סטטוס.

```csharp
public abstract class StatusValidationTaskHandlerBase : ITaskHandler
{
    protected StatusValidationTaskHandlerBase(
        IReadOnlyDictionary<int, Func<string, ValidationResult>> statusValidators);

    public abstract string TaskType { get; }
    public abstract int FinalStatus { get; }
}
```

המחלקה:
- מפעילה Validator לפי `nextStatus`
- מחזירה הצלחה כאשר אין Validator ייעודי לסטטוס
- מונעת מעבר קדימה אחרי `FinalStatus`

---

### 3. **ProcurementTaskHandler**

**סטטוס סופי:** 3

| סטטוס | דרישה | דוגמה JSON |
|-------|-------|-----------|
| 0 | - | - |
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
| 0 | - | - |
| 1 | - | - |
| 2 | **טקסט אפיון** (min 10 תווים) | `{"specification": "יש לפתח..."}` |
| 3 | **שם בראנץ'** תקין | `{"specification": "...", "branchName": "feature/xyz"}` |
| 4 | **מספר גרסה** (SemVer) | `{"...", "versionNumber": "1.2.0"}` |

**וולידציה:**
- בסטטוס 2: בדיקה שדה `specification` עם לפחות 10 תווים
- בסטטוס 3: בדיקה שדה `branchName` תקין (ללא `//', `..`, רווחים וכו')
- בסטטוס 4: בדיקה שדה `versionNumber` בפורמט SemVer

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

---

### 6. **TaskWorkflowService Integration**

```csharp
public interface ITaskWorkflowService
{
    Task<WorkflowResult> ChangeStatusAsync(
        int taskId,
        int newStatus,
        string newDataJson,
        CancellationToken cancellationToken = default);
}
```

**עבודה:**
1. `TasksController` קורא ל-`ITaskApplicationService`
2. `TaskApplicationService` מעביר פעולות Workflow ל-`ITaskWorkflowService`
3. `TaskWorkflowService` בודק JSON תקין, כללי תנועה וסטטוס סופי
4. אם קיים Handler עבור `task.TaskType`, השירות קורא ל-`ValidateStatusChange`
5. התוצאה חוזרת ל-Controller דרך שכבת ה-Application Service

---

## 🔑 עקרונות SOLID שמומשו

### 1. **Open/Closed Principle** ✅

```
פתוח להרחבה:
  - אפשר להוסיף Handler חדש (TestingTaskHandler) 
    בלי לשנות קוד קיים

סגור לשינוי:
  - TaskHandlerFactory לא משתנה
  - TaskWorkflowService לא משתנה
  - BaseTask לא משתנה
```

**דוגמה - הוספת Handler חדש:**
```csharp
// 1. יצירת Handler חדש
public class TestingTaskHandler : StatusValidationTaskHandlerBase
{
    public TestingTaskHandler()
        : base(new Dictionary<int, Func<string, ValidationResult>>
        {
            [2] = ValidateStatusTwo
        })
    {
    }

    public string TaskType => "Testing";
    public int FinalStatus => 2;
    private static ValidationResult ValidateStatusTwo(string newDataJson) { ... }
}

// 2. מקם ב-namespace DanTaskManager.Domain.Handlers

// 3. זהו! AddTaskHandlersFromAssembly ירשום אותו אוטומטית
```

---

### 2. **Single Responsibility Principle** ✅

- **ITaskHandler**: אחראי רק לוולידציה ספציפית של סוג משימה
- **TaskHandlerFactory**: אחראי רק ליצור את ה-Handler הנכון
- **TaskWorkflowService**: אחראי לכללי תנועה, JSON תקין ושמירת שינויי workflow
- **Application Services**: אחראים לתיאום בין HTTP, EF ו-workflow

---

### 3. **Dependency Inversion Principle** ✅

- התוכנה תלויה בממשקים (`ITaskHandler`, `ITaskWorkflowService`, `ITaskApplicationService`)
- לא בממשקים (`ProcurementTaskHandler`, `DevelopmentTaskHandler`)

---

## 💾 Dependency Injection - Program.cs

```csharp
// הרשמה אוטומטית של כל ה-Handlers תחת DanTaskManager.Domain.Handlers
builder.Services.AddTaskHandlersFromAssembly(typeof(ITaskHandler).Assembly);

// הרשמה של Factory (אוטומטי מזריק את כל ה-Handlers)
builder.Services.AddSingleton(sp => 
    new TaskHandlerFactory(sp.GetRequiredService<IEnumerable<ITaskHandler>>()));

// הרשמה של Services
builder.Services.AddScoped<ITaskStatusService, TaskStatusService>();
builder.Services.AddScoped<ITaskWorkflowService, TaskWorkflowService>();
builder.Services.AddScoped<ITaskApplicationService, TaskApplicationService>();
builder.Services.AddScoped<IUserApplicationService, UserApplicationService>();
```

`AddTaskHandlersFromAssembly` מגלה מחלקות לא-abstract שמממשות `ITaskHandler` ונמצאות ב-namespace `DanTaskManager.Domain.Handlers`.

---

## 📊 REST API Endpoints

### שינוי סטטוס עם וולידציה

```http
POST /api/tasks/{id}/change-status
Content-Type: application/json

{
  "newStatus": 2,
  "newDataJson": "{\"prices\": [\"5000 ₪\", \"4800 ₪\"]}"
}
```

**תוצאה בהצלחה (200):**
```json
{
  "success": true,
  "message": "סטטוס עודכן בהצלחה ל-2",
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
    ├── StatusValidationTaskHandlerBase.cs // Base class לוולידציה לפי סטטוס
    ├── ProcurementTaskHandler.cs       // Implementation
    ├── DevelopmentTaskHandler.cs       // Implementation
    └── TaskHandlerFactory.cs           // Factory

Services/
├── ITaskStatusService.cs               // ממשק
├── TaskStatusService.cs                // Implementation
├── TaskWorkflowService.cs              // Workflow orchestration
├── ITaskApplicationService.cs          // API-facing task operations
├── TaskApplicationService.cs           // Task application layer
├── IUserApplicationService.cs          // API-facing user operations
├── UserApplicationService.cs           // User application layer
└── TaskHandlerRegistrationExtensions.cs // Auto-registration

Controllers/
├── TasksController.cs                  // Thin HTTP layer
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
   public class TestingTaskHandler : StatusValidationTaskHandlerBase
   ```

2. **הטמע את ITaskHandler**
   - `TaskType` (לדוגמה: "Testing")
   - `FinalStatus` (לדוגמה: 2)
   - העבר Dictionary של `status -> validator` ל-base constructor

3. **מקם תחת `DanTaskManager.Domain.Handlers`**
   - אין צורך להוסיף `AddTransient` ב-`Program.cs`
   - `AddTaskHandlersFromAssembly` ירשום אותו אוטומטית

4. **סיום!** `TaskHandlerFactory` ו-`TaskWorkflowService` יעבדו עם ה-Handler החדש

---

## 🎓 משהו לדעת

- **CustomDataJson**: שדה JSON גמיש לנתונים המשתנים לפי סוג משימה
- **FinalStatus**: סטטוס סופי - משימה לא יכולה להתקדם מעבר לו
- **Validation**: וולידציה מתבצעת בסטטוס מסוים, לא בכולם
- **Case-insensitive**: TaskType מכופה case-insensitive

---

**מעולה! 🎉**

# 🎯 Strategy Pattern & Task Handlers - תיעוד

## 📋 מבט כללי

הפרויקט מנצל את **Strategy Pattern** עם **Factory** כדי לאפשר הרחבה של סוגי משימות חדשים ללא שינוי קוד קיים (**Open/Closed Principle**).

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
│          ITaskStatusService                             │
│  - ValidateAndChangeStatus()                           │
│  - GetFinalStatus()                                    │
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

### 2. **ProcurementTaskHandler**

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

### 3. **DevelopmentTaskHandler**

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

### 4. **TaskHandlerFactory**

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

### 5. **ITaskStatusService**

```csharp
public interface ITaskStatusService
{
    TaskStatusChangeResult ValidateAndChangeStatus(
        BaseTask task,
        int nextStatus,
        string newDataJson);
        
    int? GetFinalStatus(string taskType);
}
```

**עבודה:**
1. קובל משימה, סטטוס בא, JSON חדש
2. מוצא את ה-Handler לפי `task.TaskType`
3. קורא ל-`ValidateStatusChange` דרך Handler
4. מחזיר תוצאה (הצלחה/כישלון)

---

## 🔑 עקרונות SOLID שמומשו

### 1. **Open/Closed Principle** ✅

```
פתוח להרחבה:
  - אפשר להוסיף Handler חדש (TestingTaskHandler) 
    בלי לשנות קוד קיים

סגור לשינוי:
  - TaskHandlerFactory לא משתנה
  - ITaskStatusService לא משתנה
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

// 2. הרשמה בـ Program.cs
builder.Services.AddTransient<ITaskHandler, TestingTaskHandler>();

// 3. זהו! TaskHandlerFactory ילקח אותו אוטומטי
```

---

### 2. **Single Responsibility Principle** ✅

- **ITaskHandler**: אחראי רק לוולידציה ספציפית של סוג משימה
- **TaskHandlerFactory**: אחראי רק ליצור את ה-Handler הנכון
- **ITaskStatusService**: אחראי רק לתנסיק השינוי

---

### 3. **Dependency Inversion Principle** ✅

- התוכנה תלויה בממשקים (`ITaskHandler`, `ITaskStatusService`)
- לא בממשקים (`ProcurementTaskHandler`, `DevelopmentTaskHandler`)

---

## 💾 Dependency Injection - Program.cs

```csharp
// הרשמה של כל ה-Handlers
builder.Services.AddTransient<ITaskHandler, ProcurementTaskHandler>();
builder.Services.AddTransient<ITaskHandler, DevelopmentTaskHandler>();

// הרשמה של Factory (אוטומטי מזריק את כל ה-Handlers)
builder.Services.AddSingleton(sp => 
    new TaskHandlerFactory(sp.GetRequiredService<IEnumerable<ITaskHandler>>()));

// הרשמה של Service
builder.Services.AddScoped<ITaskStatusService, TaskStatusService>();
```

---

## 📊 REST API Endpoints

### שינוי סטטוס עם וולידציה

```http
POST /api/tasks/{id}/change-status
Content-Type: application/json

{
  "nextStatus": 2,
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
    ├── ProcurementTaskHandler.cs       // Implementation
    ├── DevelopmentTaskHandler.cs       // Implementation
    └── TaskHandlerFactory.cs           // Factory

Services/
├── ITaskStatusService.cs               // ממשק
└── TaskStatusService.cs                // Implementation

Controllers/
├── TasksController.cs                  // חדש: change-status endpoint
└── UsersController.cs
```

---

## 📖 דוגמאות שימוש

ראה [STRATEGY_EXAMPLES.cs](STRATEGY_EXAMPLES.cs) לדוגמאות קוד מלאות של:
1. שימוש ישיר ב-Handlers
2. Procurement flow
3. Development flow
4. TaskStatusService
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

3. **הוסף הרשמה ב-Program.cs**
   ```csharp
   builder.Services.AddTransient<ITaskHandler, TestingTaskHandler>();
   ```

4. **סיום!** TaskHandlerFactory וITaskStatusService יעבדו אוטומטי

---

## 🎓 משהו לדעת

- **CustomDataJson**: שדה JSON גמיש לנתונים המשתנים לפי סוג משימה
- **FinalStatus**: סטטוס סופי - משימה לא יכולה להתקדם מעבר לו
- **Validation**: וולידציה מתבצעת בסטטוס מסוים, לא בכולם
- **Case-insensitive**: TaskType מכופה case-insensitive

---

**מעולה! 🎉**

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
    ┌────┴───────┬──────────────────────┬──────────────────────┬──────────────────────┐
    │            │                      │                      │                      │
    ▼            ▼                      ▼                      ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ Procurement     │ │ Development     │ │ Analysis        │ │ Testing         │
│ TaskHandler     │ │ TaskHandler     │ │ TaskHandler     │ │ TaskHandler     │
│ FinalStatus: 3  │ │ FinalStatus: 4  │ │ FinalStatus: 2  │ │ FinalStatus: 3  │
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

### 4. **AnalysisTaskHandler**

**סטטוס סופי:** 2

| סטטוס | דרישה | דוגמה JSON |
|-------|-------|-----------|
| 0 | - | - |
| 1 | - | - |
| 2 | **דוח ניתוח** לא ריק | `{"analysisReport": "Reviewed scope and risks."}` |

**וולידציה:**
- בסטטוס 2: בדיקה שקיים שדה `analysisReport` מסוג מחרוזת ושאינו ריק.
- ניסיון להתקדם מעבר לסטטוס 2 נכשל כי זהו ה-`FinalStatus`.

---

### 5. **TestingTaskHandler**

**סטטוס סופי:** 3

| סטטוס | דרישה | דוגמה JSON |
|-------|-------|-----------|
| 0 | - | - |
| 1 | - | - |
| 2 | מספר מקרי בדיקה גדול מ-0 | `{"testCases": 15}` |
| 3 | אחוז כיסוי תקין וסיכום לא ריק | `{"coverage": "85%", "summary": "Regression completed"}` |

**וולידציה:**
- בסטטוס 2: בדיקה ש-`testCases` הוא מספר שלם גדול מ-0.
- בסטטוס 3: בדיקה ש-`coverage` הוא מחרוזת בפורמט אחוזים בין 0% ל-100%, וש-`summary` הוא מחרוזת לא ריקה.
- ניסיון להתקדם מעבר לסטטוס 3 נכשל כי זהו ה-`FinalStatus`.

---

### 6. **TaskHandlerFactory**

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
- אם אין Handler, `GetHandler()` מחזיר `null`; יצירת משימה ושינוי סטטוס דוחים סוגים לא רשומים במקום להריץ fallback בסיסי
- `TaskType` כפול (גם בשינוי רישיות בלבד) יגרום לכשל בבניית המפה, ולכן כל Handler חייב שם ייחודי

---

### 7. **ITaskStatusService**

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

// 2. אין צורך לעדכן Program.cs אם המחלקה נמצאת ב-Domain.Handlers,
//    מממשת ITaskHandler, אינה abstract, ושמה מסתיים ב-TaskHandler.

// 3. AddTaskHandlersFromAssembly יזהה אותה, ו-TaskHandlerFactory יקבל אותה דרך DI
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
// סריקה והרשמה של כל ה-Handlers מתוך assembly של Domain.Handlers
builder.Services.AddTaskHandlersFromAssembly();

// הרשמה של Factory (מקבל IEnumerable<ITaskHandler> מה-DI)
builder.Services.AddScoped<TaskHandlerFactory>();

// הרשמה של Service
builder.Services.AddScoped<ITaskStatusService, TaskStatusService>();
```

### כללי גילוי אוטומטי

`AddTaskHandlersFromAssembly()` רושם רק מחלקות שעומדות בכל התנאים הבאים:

1. מממשות `ITaskHandler`.
2. נמצאות באותו namespace של `ITaskHandler` (`DanTaskManager.Domain.Handlers`).
3. הן concrete classes (`IsClass == true`, `IsAbstract == false`).
4. שם המחלקה מסתיים ב-`TaskHandler`.

אם מוסיפים Handler תחת namespace אחר או בשם שלא מסתיים ב-`TaskHandler`, הוא לא יירשם אוטומטית ו-API יציג אותו כסוג לא נתמך.

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

### יצירת משימה עם סוג לא נתמך

```http
POST /api/tasks
Content-Type: application/json

{
  "taskType": "Unknown",
  "description": "משימה ללא Handler",
  "assignedToUserId": 1
}
```

**תוצאה בכישלון (400):**
```json
{
  "error": "TaskType לא נתמך: Unknown",
  "supportedTaskTypes": ["Analysis", "Development", "Procurement", "Testing"]
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
    ├── ITaskHandler.cs                         // ממשק
    ├── ProcurementTaskHandler.cs               // Implementation
    ├── DevelopmentTaskHandler.cs               // Implementation
    ├── AnalysisTaskHandler.cs                  // Implementation
    ├── TestingTaskHandler.cs                   // Implementation
    ├── TaskHandlerFactory.cs                   // Factory
    └── TaskHandlerRegistrationExtensions.cs    // DI auto-registration

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

3. **ודא שהמחלקה ניתנת לגילוי אוטומטי**
   - namespace: `DanTaskManager.Domain.Handlers`
   - שם מחלקה שמסתיים ב-`TaskHandler`
   - concrete class שמממשת `ITaskHandler`

4. **הוסף בדיקות**
   - בדיקות יחידה ל-Handler החדש
   - בדיקת workflow לסוג לא נתמך או לנתונים חסרים אם יש סיכון תפעולי

5. **סיום!** `AddTaskHandlersFromAssembly`, `TaskHandlerFactory` ו-`ITaskStatusService` יעבדו אוטומטית

---

## 🎓 משהו לדעת

- **CustomDataJson**: שדה JSON גמיש לנתונים המשתנים לפי סוג משימה
- **FinalStatus**: סטטוס סופי - משימה לא יכולה להתקדם מעבר לו
- **Validation**: וולידציה מתבצעת בסטטוס מסוים, לא בכולם
- **Case-insensitive**: TaskType מכופה case-insensitive
- **Unsupported TaskType**: יצירת משימה ושינוי סטטוס נכשלים אם אין Handler רשום
- **Auto-registration**: אין צורך לעדכן `Program.cs` עבור Handler חדש שעומד בכללי הגילוי

---

**מעולה! 🎉**

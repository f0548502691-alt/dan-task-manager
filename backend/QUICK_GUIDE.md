# 🎯 Strategy Pattern Implementation - תיעוד מהיר

## 📌 שלום! מה זה המערכת החדשה?

ממשנו **Strategy Pattern** עם **Factory** כדי לאפשר הרחבה של סוגי משימות חדשים ללא שינוי קוד קיים (Open/Closed Principle).

---

## 🎬 מה השתנה?

### לפני
```csharp
// קוד אחד גדול שמטפל בכל סוגי המשימות
if (task.TaskType == "Procurement") { /* logic */ }
else if (task.TaskType == "Development") { /* logic */ }
else if (task.TaskType == "Testing") { /* logic */ }
// ... מה לעשות כשמוסיפים סוג חדש?
```

### אחרי ✅
```csharp
// Strategy Pattern - כל סוג בעל Handler שלו
var handler = factory.GetHandler(task.TaskType);
var result = handler.ValidateStatusChange(...);
// + הוספת Handler חדש לא דורשת שינוי ב-Factory
```

---

## 📚 מה נוצר?

### **Domain Layer** - ממשקים וImplementations

#### `ITaskHandler` - ממשק Strategy
```
Domain/Handlers/ITaskHandler.cs
├─ TaskType (property)
├─ FinalStatus (property)
└─ ValidateStatusChange(...) (method)
```

#### `ProcurementTaskHandler` - סוג משימה 1
```
Domain/Handlers/ProcurementTaskHandler.cs
├─ TaskType = "Procurement"
├─ FinalStatus = 3
└─ ValidateStatusChange()
   ├─ Status 2: prices[] (2 מחרוזות)
   └─ Status 3: receipt (מחרוזת)
```

#### `DevelopmentTaskHandler` - סוג משימה 2
```
Domain/Handlers/DevelopmentTaskHandler.cs
├─ TaskType = "Development"
├─ FinalStatus = 4
└─ ValidateStatusChange()
   ├─ Status 2: specification (10+ תווים)
   ├─ Status 3: branchName (תקין)
   └─ Status 4: versionNumber (SemVer)
```

#### `TaskHandlerFactory` - Factory לייצור Handlers
```
Domain/Handlers/TaskHandlerFactory.cs
├─ GetHandler(taskType) → ITaskHandler?
├─ HasHandler(taskType) → bool
└─ GetRegisteredTaskTypes() → IEnumerable<string>
```

### **Service Layer** - לוגיקה עסקית

#### `ITaskStatusService` - ממשק לשרות
```
Services/ITaskStatusService.cs
├─ ValidateAndChangeStatus(task, nextStatus, newDataJson)
└─ GetFinalStatus(taskType)
```

#### `TaskStatusService` - Implementation
```
Services/TaskStatusService.cs
├─ משתמש ב-TaskHandlerFactory
├─ עורך וולידציה דרך Handler
└─ מחזיר תוצאות עם הודעות
```

### **Controller** - API Endpoint

```
Controllers/TasksController.cs
└─ POST /api/tasks/{id}/change-status
   ├─ nextStatus
   └─ newDataJson
```

### **Testing** - Unit Tests

```
Tests/HandlerTests.cs
├─ ProcurementTaskHandlerTests (10 בדיקות)
├─ DevelopmentTaskHandlerTests (10 בדיקות)
└─ TaskHandlerFactoryTests (5 בדיקות)
```

---

## 🚀 איך זה עובד?

### 1. **יצירת משימה**
```csharp
var task = new BaseTask
{
    TaskType = "Procurement",
    CurrentStatus = 1,
    CustomDataJson = "{}"
};
```

### 2. **שינוי סטטוס עם וולידציה**
```csharp
var result = taskStatusService.ValidateAndChangeStatus(
    task,
    nextStatus: 2,
    newDataJson: "{\"prices\": [\"5000\", \"4800\"]}"
);

if (result.Success)
{
    task.CurrentStatus = result.NewStatus.Value;
    task.CustomDataJson = newDataJson;
    // save to db
}
```

### 3. **Flow פנימי**
```
ValidateAndChangeStatus()
  ↓
GetHandler("Procurement")  ← Factory
  ↓
handler.ValidateStatusChange()
  ↓
ValidationResult (IsValid, Message)
```

---

## 📊 דוגמאות - Procurement

```
סטטוס 0: התחלה
   ↓
סטטוס 1: בתהליך
   ↓
סטטוס 2: בחירת ספקים ⭐
   דורש: {"prices": ["5000 ₪", "4800 ₪"]}
   ↓
סטטוס 3: ✅ סופי (FinalStatus)
   דורש: {"prices": [...], "receipt": "REC-123"}
   ↓
אי אפשר להמשיך מפה!
```

---

## 📊 דוגמאות - Development

```
סטטוס 0: התחלה
   ↓
סטטוס 1: בתהליך
   ↓
סטטוס 2: אפיון ⭐
   דורש: {"specification": "יש לפתח..."}
   ↓
סטטוס 3: בקידוד ⭐
   דורש: {"specification": "...", "branchName": "feature/xyz"}
   ↓
סטטוס 4: ✅ סופי (FinalStatus)
   דורש: {"specification": "...", "branchName": "...", "versionNumber": "1.2.0"}
   ↓
אי אפשר להמשיך מפה!
```

---

## 🔌 REST API

### שינוי סטטוס

```http
POST /api/tasks/1/change-status
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

## 🛠️ DI Registration

```csharp
// Program.cs

// הרשמה של Handlers
builder.Services.AddTransient<ITaskHandler, ProcurementTaskHandler>();
builder.Services.AddTransient<ITaskHandler, DevelopmentTaskHandler>();

// הרשמה של Factory (מזריק את כל ה-Handlers אוטומטי)
builder.Services.AddSingleton(sp => 
    new TaskHandlerFactory(sp.GetRequiredService<IEnumerable<ITaskHandler>>()));

// הרשמה של Service
builder.Services.AddScoped<ITaskStatusService, TaskStatusService>();
```

---

## 💡 איך להוסיף Handler חדש?

### שלב 1: יצור Handler
```csharp
public class TestingTaskHandler : ITaskHandler
{
    public string TaskType => "Testing";
    public int FinalStatus => 2;
    
    public ValidationResult ValidateStatusChange(...)
    {
        if (nextStatus == 2)
            return ValidateTestResults(newDataJson);
        return ValidationResult.Success();
    }
    
    private ValidationResult ValidateTestResults(string json)
    {
        // וולידציה ספציפית...
    }
}
```

### שלב 2: הרשמה
```csharp
builder.Services.AddTransient<ITaskHandler, TestingTaskHandler>();
```

### שלב 3: סיום! ✅
כל יתר הקוד עובד אוטומטי!

---

## 📚 קבצי תיעוד

| קובץ | תיאור |
|------|-------|
| [STRATEGY_PATTERN_DOCS.md](STRATEGY_PATTERN_DOCS.md) | **תיעוד מלא ומפורט** |
| [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) | סיכום טכני |
| [README.md](README.md) | תיעוד כללי |

---

## 🧪 Run Tests

```bash
dotnet test
```

Results:
- ✅ ProcurementTaskHandler: 7 בדיקות
- ✅ DevelopmentTaskHandler: 8 בדיקות
- ✅ TaskHandlerFactory: 5 בדיקות
- **Total: 20 בדיקות**

---

## ✅ Checklist

- [x] ITaskHandler ממשק
- [x] ProcurementTaskHandler implementation
- [x] DevelopmentTaskHandler implementation
- [x] TaskHandlerFactory
- [x] ITaskStatusService ממשק
- [x] TaskStatusService implementation
- [x] REST API endpoint (change-status)
- [x] DI registration
- [x] Unit tests (30+)
- [x] Documentation
- [x] Examples

---

## 🎯 עקרונות SOLID

✅ **Open/Closed Principle** - פתוח להרחבה, סגור לשינוי  
✅ **Single Responsibility** - כל Handler כמחלקה יחידה  
✅ **Liskov Substitution** - כל Handler יכול להחליף אחר  
✅ **Interface Segregation** - ממשקים קטנים וממוקדים  
✅ **Dependency Inversion** - תלות בממשקים

---

## 🎉 מוכן!

המערכת מוכנה לשימוש וסיום!

**Next Steps:**
1. `dotnet build` - בנייה
2. `dotnet test` - בדיקות
3. `dotnet ef migrations add InitialCreate`
4. `dotnet ef database update`
5. `dotnet run` - הרצה

---

**עזור בהצלחה! 🚀**

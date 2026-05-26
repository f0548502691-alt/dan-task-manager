# מנהל משימות - DanTaskManager

פרויקט .NET 8 עם EF Core ו-SQL Server לניהול משימות גנרי.

## 📋 מבנה הפרויקט

```
dan-task-manager/
├── Controllers/
│   ├── TasksController.cs      # REST API למשימות ול-workflow
│   └── TaskTypesController.cs  # API ל-metadata של סוגי משימות
├── Domain/
│   ├── AppUser.cs              # מחלקה המייצגת משתמש
│   ├── BaseTask.cs             # מחלקה למשימה בסיסית
│   └── Handlers/               # ITaskHandler ומימושי fallback
├── Data/
│   └── ApplicationDbContext.cs  # DbContext עם הגדרות EF Core
├── Services/
│   ├── TaskApplicationService.cs
│   ├── TaskWorkflowService.cs
│   └── TaskTypeValidationService.cs
├── DanTaskManager.csproj   # קובץ הפרויקט
└── README.md
```

## 🏗️ מחלקות Domain

### AppUser
ייצוג משתמש במערכת:
- `Id`: מזהה ייחודי
- `Name`: שם המשתמש
- `Email`: דוא"ל (עם אינדקס ייחודי)
- `CreatedAt`: תאריך יצירה
- `Tasks`: קשר לרבות משימות

### BaseTask
ייצוג משימה עם תמיכה בנתונים משתנים:
- `Id`: מזהה ייחודי
- `TaskType`: סוג המשימה (Analysis, Development, Testing, וכו')
- `CurrentStatus`: סטטוס workflow כמספר. `1` הוא סטטוס יצירה/התחלה, `99` הוא סגור.
- `AssignedToUserId`: מזהה המשתמש המופקד
- `AssignedToUser`: קשר למשתמש
- `Description`: תיאור המשימה
- **`CustomDataJson`**: JSON המכיל נתונים משתנים בהתאם לסוג המשימה
- `CreatedAt` / `UpdatedAt`: ניהול תאריכים

## 💾 DbContext - ApplicationDbContext

ההגדרות כוללות:

### תכונות JSON
`CustomDataJson` מוגדר כעמודת JSON מסוג `nvarchar(max)` התומכת בשמירת נתונים דינאמיים:

```csharp
taskBuilder
    .Property(t => t.CustomDataJson)
    .IsRequired()
    .HasColumnType("nvarchar(max)")
    .HasDefaultValue("{}");
```

### Seed Data - 6 משתמשים
כברירת מחדל, יש 6 משתמשים בסיסיים:
1. **דן כהן** (dan@example.com)
2. **רות לוי** (ruth@example.com)
3. **משה אברהם** (moshe@example.com)
4. **נועה ישראלי** (noa@example.com)
5. **איתן ברק** (eitan@example.com)
6. **מיכל גל** (michal@example.com)

וכן משימות וסוגי משימות לדוגמה. סוגי המשימות `Procurement` ו-`Development` כוללים metadata
לסטטוס סופי ולשדות חובה לפי סטטוס.

## 🔧 Setup והגדרה

### 1. התקנת Packages
```bash
dotnet restore
```

### 2. הגדרת Connection String
בקובץ `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=DanTaskManager;Trusted_Connection=true;Encrypt=false;"
  }
}
```

### 3. יצירת Migration
```bash
dotnet ef migrations add InitialCreate
```

### 4. עדכון בסיס הנתונים
```bash
dotnet ef database update
```

## 📝 דוגמה לשימוש ב-CustomDataJson

```csharp
// יצירת משימה עם נתונים משתנים
var task = new BaseTask
{
    TaskType = "Analysis",
    Description = "ניתוח",
    AssignedToUserId = 1,
    CustomDataJson = @"{
        ""priority"": ""high"",
        ""deadline"": ""2026-06-15"",
        ""estimatedHours"": 8,
        ""customField"": ""ערך"""
};

// שמירה בדטה בייס
context.Tasks.Add(task);
await context.SaveChangesAsync();
```

## 🔁 Workflow API - כללים עיקריים

- יצירת משימה מתחילה ב-`CurrentStatus = 1`.
- שינוי סטטוס מתבצע דרך `POST /api/tasks/{id}/change-status` עם `newStatus`,
  `nextAssignedToUserId` ו-`customFields` כאובייקט JSON.
- תנועה קדימה חייבת להיות בדיוק +1; rollback מותר לכל סטטוס נמוך יותר כל עוד הוא `>= 1`.
- סגירה מתבצעת רק דרך `POST /api/tasks/{id}/close` ודורשת `nextAssignedToUserId` ו-`finalNotes`.
- `FinalStatus` של סוג משימה חייב להיות בין `1` ל-`98`; `99` שמור למשימות סגורות.

ראו פירוט מלא ב-[WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md).

## 📖 הערות חשובות

- **JSON Columns**: EF Core 8 תומך בעבודה עם JSON columns בצורה מובנית
- **Foreign Keys**: קשר One-to-Many בין AppUser ו-BaseTask עם `OnDelete(DeleteBehavior.Restrict)`
- **Indexes**: יצירת אינדקס על `Email` ו-`TaskType` להאצת חיפושים
- **Timestamps**: `CreatedAt` ו-`UpdatedAt` מוגדרים עם `GETUTCDATE()` כברירת מחדל

## 🚀 שלבים הבאים

1. יצירת Controllers לקבלת בקשות API
2. הוספת Business Logic שכבה
3. מימוש סינון וחיפוש משימות לפי סוג וסטטוס
4. הוספת Validation ל-CustomDataJson
5. יצירת Migration ו-seed לנתונים נוספים

---

**נבנה עם:** .NET 8, EF Core 8, SQL Server

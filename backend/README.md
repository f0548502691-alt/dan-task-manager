# מנהל משימות - DanTaskManager

פרויקט .NET 8 עם EF Core ו-SQL Server לניהול משימות גנרי.

## 📋 מבנה הפרויקט

```
dan-task-manager/
├── Domain/
│   ├── AppUser.cs          # מחלקה המייצגת משתמש
│   ├── BaseTask.cs         # מחלקה למשימה בסיסית
│   └── Handlers/           # Strategy pattern לסוגי משימות
├── Services/               # Application services ו-workflow
├── Controllers/            # REST API
├── Data/
│   └── ApplicationDbContext.cs  # DbContext עם הגדרות EF Core
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
- `CurrentStatus`: סטטוס מספרי לפי ה-workflow של סוג המשימה; `99` מייצג משימה סגורה
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

### Seed Data - 3 משתמשים
כברירת מחדל, יש 3 משתמשים בסיסיים:
1. **דן כהן** (dan@example.com)
2. **רות לוי** (ruth@example.com)
3. **משה אברהם** (moshe@example.com)

וכן 3 משימות לדוגמה עם `CustomDataJson` שונה לכל אחת.

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

## 📖 הערות חשובות

- **JSON Columns**: EF Core 8 תומך בעבודה עם JSON columns בצורה מובנית
- **Foreign Keys**: קשר One-to-Many בין AppUser ו-BaseTask עם `OnDelete(DeleteBehavior.Restrict)`
- **Indexes**: יצירת אינדקס על `Email` ו-`TaskType` להאצת חיפושים
- **Timestamps**: `CreatedAt` ו-`UpdatedAt` מוגדרים עם `GETUTCDATE()` כברירת מחדל

## 🚀 שלבים הבאים

1. להריץ את ה-API עם `dotnet run`
2. לבדוק את נקודות הקצה תחת `/api/tasks` ו-`/api/users`
3. להוסיף סוג משימה חדש על ידי Handler תחת `Domain/Handlers`
4. לעיין ב-[GETTING_STARTED.md](GETTING_STARTED.md) וב-[EXTENSION_GUIDE.md](EXTENSION_GUIDE.md) להרחבות

---

**נבנה עם:** .NET 8, EF Core 8, SQL Server

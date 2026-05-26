# 🚀 מדריך התחלה מהיר - DanTaskManager

## ✅ מה נוצר

### 📦 מחלקות Domain
- [Domain/AppUser.cs](Domain/AppUser.cs) - מחלקת המשתמש עם ID, Name, Email
- [Domain/BaseTask.cs](Domain/BaseTask.cs) - מחלקת המשימה עם CustomDataJson

### 💾 DbContext  
- [Data/ApplicationDbContext.cs](Data/ApplicationDbContext.cs) - EF Core context עם:
  - הגדרת JSON columns ל-CustomDataJson
  - Seed data של 3 משתמשים
  - 3 משימות לדוגמה
  - Relationships וindexes

### 🔧 קונפיגורציה
- [DanTaskManager.csproj](DanTaskManager.csproj) - .NET 8 עם NuGet packages
- [Program.cs](Program.cs) - Dependency Injection ו-DbContext registration
- [appsettings.json](appsettings.json) - Connection string לSQL Server

### 🎮 Controllers (Bonus)
- [Controllers/TasksController.cs](Controllers/TasksController.cs) - REST API for Tasks
- [Controllers/UsersController.cs](Controllers/UsersController.cs) - read-only REST API for seeded users and their tasks

### 📚 דוקומנטציה
- [README.md](README.md) - תיעוד מלא בעברית
- [EXAMPLES.cs](EXAMPLES.cs) - דוגמאות קוד שימושיות

---

## 🏃 שלבי התחלה

### 1️⃣ התקנת Packages
```bash
dotnet restore
```

### 2️⃣ הגדרת בסיס הנתונים
ודא ש-`appsettings.json` מצביע לSQL Server שלך:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=DanTaskManager;Trusted_Connection=true;Encrypt=false;"
  }
}
```

### 3️⃣ יצירת Migration
```bash
dotnet ef migrations add InitialCreate
```

### 4️⃣ עדכון בסיס הנתונים (Create DB)
```bash
dotnet ef database update
```

### 5️⃣ הרצת האפליקציה
```bash
dotnet run
```

---

## 📋 מבנה הפרויקט

```
dan-task-manager/
├── Domain/
│   ├── AppUser.cs           # מחלקה: משתמש
│   └── BaseTask.cs          # מחלקה: משימה עם CustomDataJson
├── Data/
│   └── ApplicationDbContext.cs # DbContext עם Seed data
├── Controllers/
│   ├── TasksController.cs   # API endpoints for tasks
│   └── UsersController.cs   # API endpoints for users
├── DanTaskManager.csproj    # .NET 8 project file
├── Program.cs               # DI Configuration
├── appsettings.json         # Connection strings
├── EXAMPLES.cs              # דוגמאות שימוש
└── README.md                # תיעוד מלא
```

---

## 🎯 עכשיו אתה יכול:

✅ **שאילתות בסיסיות:**
```csharp
// קבלת כל המשתמשים
var users = await context.Users.ToListAsync();

// קבלת משימה לפי ID
var task = await context.Tasks.FindAsync(1);

// קבלת משימות בסטטוס "בתהליך"
var inProgress = await context.Tasks
    .Where(t => t.CurrentStatus == 1)
    .ToListAsync();
```

✅ **עבודה עם CustomDataJson:**
```csharp
var customData = new { priority = "high", deadline = "2026-06-15" };
task.CustomDataJson = JsonSerializer.Serialize(customData);
```

✅ **REST API Endpoints:**
- `GET /api/tasks` - קבלת כל המשימות
- `POST /api/tasks` - יצירת משימה חדשה
- `PUT /api/tasks/{id}` - עדכון משימה
- `DELETE /api/tasks/{id}` - מחיקת משימה
- `GET /api/users?page=1&pageSize=20` - קבלת משתמשים קיימים
- `GET /api/users/{id}` - פרטי משתמש קיים
- `GET /api/users/{id}/tasks?page=1&pageSize=20` - משימות של משתמש

> אין `POST /api/users`: משתמשים מגיעים מ-seed/migrations או מ-workflow ייעודי שתוסיפו במפורש.

---

## 📝 הערות חשובות

1. **SQL Server Connection**: תעדכן את ה-connection string ב-`appsettings.json`

2. **CustomDataJson**: 
   - מאוחסן כ-`nvarchar(max)` בבסיס הנתונים
   - ברירת מחדל: `"{}"`
   - ניתן להכניס כל JSON שהוא

3. **Status Values**:
   - `0` = לא התחילה
   - `1` = בתהליך
   - `2` = הושלמה
   - `3` = ביוטלה

4. **Seed Data**: 
   - 3 משתמשים נטועים באופן אוטומטי
   - 3 משימות לדוגמה
   - רץ בעת `database update`

5. **Users API**:
   - מיועד לקריאה בלבד
   - pagination משתמש ב-`page` ו-`pageSize`
   - `pageSize` מוגבל ל-100 ומנורמל ל-20 אם נשלח ערך קטן מ-1

---

## 🔗 לקריאה נוספת

- [Entity Framework Core 8 Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [JSON columns in EF Core](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions#json-columns)
- [SQL Server and JSON](https://learn.microsoft.com/en-us/sql/relational-databases/json/json-data-sql-server)

---

**Enjoy! 🎉**

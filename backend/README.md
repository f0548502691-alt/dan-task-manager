# מנהל משימות - DanTaskManager

פרויקט .NET 8 עם EF Core ו-SQL Server לניהול משימות גנרי עם Workflow מבוסס סוג משימה.

## מבנה הפרויקט

```text
backend/
├── Controllers/            # REST API עבור users/tasks
├── Data/                   # ApplicationDbContext, mapping ו-seed data
├── Domain/                 # AppUser, BaseTask, workflow errors ו-handlers
├── Middleware/             # GlobalExceptionMiddleware לפורמט שגיאות אחיד
├── Services/               # Application services, DTO projections ו-workflow orchestration
├── Tests/                  # בדיקות xUnit לשירותים ול-handlers
├── Program.cs              # DI, Swagger, EF migrations ו-routing
└── DanTaskManager.csproj
```

## מודל Domain

### AppUser

ייצוג משתמש קיים במערכת:
- `Id`: מזהה ייחודי.
- `Name`: שם המשתמש.
- `Email`: דוא"ל עם אינדקס ייחודי.
- `CreatedAt`: תאריך יצירה.
- `Tasks`: קשר One-to-Many למשימות.

### BaseTask

ייצוג משימה עם נתונים משתנים לפי סוג:
- `TaskType`: סוג המשימה, למשל `Procurement` או `Development`.
- `CurrentStatus`: סטטוס מספרי. הסטטוסים המשותפים הם `0`, `1`, `2`, `3`, `4`, וסטטוס סגירה `99`.
- `AssignedToUserId`: מזהה המשתמש שמוקצה למשימה.
- `Description`: תיאור משימה.
- `CustomDataJson`: JSON מסוג `nvarchar(max)` עם נתונים ספציפיים לסוג/סטטוס.
- `CreatedAt` / `UpdatedAt`: timestamps ב-UTC.

## Setup והרצה

### 1. התקנת packages

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

### 3. הרצת השרת

```bash
dotnet run --project DanTaskManager.csproj
```

`Program.cs` מריץ `Database.Migrate()` בזמן העלייה, לכן סביבת הרצה צריכה הרשאות יצירה/עדכון schema במסד הנתונים. אם אין migration קיים בסביבת הפיתוח, יש ליצור אחד לפני הרצה ראשונה:

```bash
dotnet ef migrations add InitialCreate
```

בסביבת Development מופעל Swagger דרך `AddSwaggerGen()`.

### 4. בדיקות

```bash
dotnet test DanTaskManager.csproj
```

## API ציבורי

כל רשימות ה-API המפולטרות מוחזרות כ-`PagedResult<T>`:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0
}
```

`PageRequest` מחזיר `page=1` כאשר נשלח ערך קטן מ-1, מחזיר `pageSize=20` כאשר נשלח ערך קטן מ-1, ומגביל `pageSize` למקסימום `100`.

### TasksController

| Method | Route | Return | הערות |
| --- | --- | --- | --- |
| `GET` | `/api/tasks?page=1&pageSize=20` | `PagedResult<TaskSummaryDto>` | כל המשימות, ממוינות מהחדש לישן |
| `GET` | `/api/tasks/{id}` | `TaskDetailsDto` | כולל `customDataJson` |
| `GET` | `/api/tasks/byType/{taskType}` | `PagedResult<TaskSummaryDto>` | סינון לפי `TaskType` |
| `GET` | `/api/tasks/user/{userId}` | `PagedResult<TaskSummaryDto>` | רק משימות פתוחות של משתמש קיים |
| `POST` | `/api/tasks` | `TaskDetailsDto` | יוצר משימה בסטטוס `0`; `customDataJson` ברירת מחדל `{}` |
| `POST` | `/api/tasks/{id}/change-status` | workflow response | מפעיל חוקי workflow ו-handler לפי סוג |
| `POST` | `/api/tasks/{id}/close` | close response | מעביר לסטטוס `99` ושומר `finalNotes`/`closedAt` ב-JSON |
| `PUT` | `/api/tasks/{id}` | `204 NoContent` | מעדכן description אם נשלח ערך לא ריק |
| `DELETE` | `/api/tasks/{id}` | `204 NoContent` | מוחק משימה |

### UsersController

| Method | Route | Return | הערות |
| --- | --- | --- | --- |
| `GET` | `/api/users?page=1&pageSize=20` | `PagedResult<UserSummaryDto>` | כולל ספירת משימות פתוחות |
| `GET` | `/api/users/{id}` | `UserDetailsDto` | מחזיר `404` אם המשתמש לא קיים |
| `GET` | `/api/users/{id}/tasks` | `PagedResult<TaskSummaryDto>` | כל משימות המשתמש, כולל סגורות |

## Workflow contracts

בקשת שינוי סטטוס:

```json
{
  "newStatus": 2,
  "newDataJson": "{\"prices\":[\"1200\",\"1350\"]}"
}
```

חוקי תנועה כלליים (`TaskWorkflowService`):
- משימה סגורה (`99`) לא ניתנת לעדכון.
- תנועה קדימה חייבת להיות בדיוק `+1`.
- תנועה אחורה מותרת לכל סטטוס נמוך יותר.
- אותו סטטוס נדחה.
- `newDataJson` חייב להיות JSON תקין ולא ריק.

### Handlers לפי סוג משימה

| TaskType | Final status | סטטוס | JSON נדרש |
| --- | --- | --- | --- |
| `Procurement` | `3` | `2` | `prices`: מערך של בדיוק 2 מחרוזות לא ריקות |
| `Procurement` | `3` | `3` | `receipt`: מחרוזת לא ריקה |
| `Development` | `4` | `2` | `specification`: מחרוזת באורך 10+ תווים |
| `Development` | `4` | `3` | `branchName`: מחרוזת לא ריקה ללא רווחים, `//`, סיומת `/`, או סיומת `.` |
| `Development` | `4` | `4` | `versionNumber`: מחרוזת או מספר לא ריקים; גרסה עם נקודות חייבת להכיל חלקים מספריים |

דוגמאות:

```json
{ "prices": ["1200", "1350"] }
{ "receipt": "PO-2026-0042" }
{ "specification": "Add CSV invoice export with filters" }
{ "branchName": "feature/invoice-export" }
{ "versionNumber": "1.2.0" }
```

## שגיאות ותפעול

- `GlobalExceptionMiddleware` מחזיר שגיאות workflow בפורמט `{ "error": "...", "code": "workflow_validation_failed" }` עם HTTP 400.
- שגיאות לא צפויות מוחזרות כ-HTTP 500 עם `code: "internal_server_error"`.
- `CreateTask` ופעולות validation בסיסיות עשויות להחזיר `BadRequest` עם `{ "error": "..." }`.
- `ApplicationDbContext` מגדיר FK מ-`BaseTask.AssignedToUserId` ל-`AppUser.Id` עם `DeleteBehavior.Restrict`.
- Seed data כולל 6 משתמשים ו-3 משימות לדוגמה.

## הערות אינטגרציה מול ה-Frontend

- `TaskDetailsDto` כולל `customDataJson`; `TaskSummaryDto` לא כולל אותו.
- `GET /api/tasks/user/{userId}` מחזיר `PagedResult<TaskSummaryDto>`, לא מערך `BaseTaskDto[]`.
- אם ה-UI צריך לערוך payload של משימה קיימת, יש לטעון פרטי משימה דרך `GET /api/tasks/{id}` או לשנות את contract של רשימת המשימות.

---

**נבנה עם:** .NET 8, EF Core 8, SQL Server, xUnit

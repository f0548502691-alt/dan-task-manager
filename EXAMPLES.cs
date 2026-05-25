// 📝 דוגמאות לעבודה עם ApplicationDbContext ו-CustomDataJson

using DanTaskManager.Data;
using DanTaskManager.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

/* ============================================
   דוגמה 1: יצירת משתמש חדש ומשימה
   ============================================ */

public class TaskManagerExamples
{
    private readonly ApplicationDbContext _context;

    public TaskManagerExamples(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// דוגמה: יצירת משתמש ומשימה חדשה
    /// </summary>
    public async Task CreateUserAndTaskExample()
    {
        // יצירת משתמש חדש
        var newUser = new AppUser
        {
            Name = "יוחנן כהן",
            Email = "yochanan@example.com"
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        // יצירת משימה עם CustomDataJson מורכב
        var customData = new
        {
            priority = "high",
            deadline = "2026-06-30",
            estimatedHours = 20,
            tags = new[] { "urgent", "api" },
            metadata = new { department = "Development", owner = "Tech Lead" }
        };

        var task = new BaseTask
        {
            TaskType = "Development",
            Description = "פיתוח API למשתמשים",
            AssignedToUserId = newUser.Id,
            CurrentStatus = 1, // בתהליך
            CustomDataJson = JsonSerializer.Serialize(customData),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();
    }

    /* ============================================
       דוגמה 2: קריאה ופענוח CustomDataJson
       ============================================ */

    public async Task ReadAndDeserializeExample()
    {
        var tasks = await _context.Tasks
            .Include(t => t.AssignedToUser)
            .ToListAsync();

        foreach (var task in tasks)
        {
            // פענוח ה-JSON
            try
            {
                var customData = JsonSerializer.Deserialize<dynamic>(task.CustomDataJson);
                
                Console.WriteLine($"משימה: {task.Description}");
                Console.WriteLine($"JSON Data: {task.CustomDataJson}");
                // ניתן לעבוד עם customData כאובייקט דינאמי
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"שגיאה בפענוח JSON: {ex.Message}");
            }
        }
    }

    /* ============================================
       דוגמה 3: עדכון CustomDataJson
       ============================================ */

    public async Task UpdateCustomDataExample(int taskId)
    {
        var task = await _context.Tasks.FindAsync(taskId);
        
        if (task != null)
        {
            // יצירת JSON חדש
            var updatedCustomData = new
            {
                priority = "critical",
                deadline = "2026-06-20",
                estimatedHours = 30,
                completedHours = 15,
                status_update = "בביצוע עקבי"
            };

            task.CustomDataJson = JsonSerializer.Serialize(updatedCustomData);
            task.UpdatedAt = DateTime.UtcNow;

            _context.Tasks.Update(task);
            await _context.SaveChangesAsync();
        }
    }

    /* ============================================
       דוגמה 4: חיפוש משימות לפי סטטוס
       ============================================ */

    public async Task QueryTasksByStatusExample()
    {
        // משימות שלא התחילו (status = 0)
        var notStarted = await _context.Tasks
            .Where(t => t.CurrentStatus == 0)
            .ToListAsync();

        // משימות בתהליך (status = 1)
        var inProgress = await _context.Tasks
            .Where(t => t.CurrentStatus == 1)
            .ToListAsync();

        // משימות הושלמות (status = 2)
        var completed = await _context.Tasks
            .Where(t => t.CurrentStatus == 2)
            .ToListAsync();

        Console.WriteLine($"לא התחילה: {notStarted.Count}");
        Console.WriteLine($"בתהליך: {inProgress.Count}");
        Console.WriteLine($"הושלמה: {completed.Count}");
    }

    /* ============================================
       דוגמה 5: חיפוש משימות לפי סוג וסטטוס
       ============================================ */

    public async Task QueryTasksByTypeAndStatusExample(string taskType, int status)
    {
        var tasks = await _context.Tasks
            .Where(t => t.TaskType == taskType && t.CurrentStatus == status)
            .Include(t => t.AssignedToUser)
            .ToListAsync();

        foreach (var task in tasks)
        {
            Console.WriteLine($"משימה: {task.Description}");
            Console.WriteLine($"משתמש: {task.AssignedToUser?.Name}");
            Console.WriteLine($"סוג: {task.TaskType}");
            Console.WriteLine($"סטטוס: {GetStatusName(task.CurrentStatus)}");
            Console.WriteLine("---");
        }
    }

    /* ============================================
       דוגמה 6: יצירת משימות מסוגים שונים
       ============================================ */

    public async Task CreateTasksWithDifferentTypesExample()
    {
        var user = await _context.Users.FirstOrDefaultAsync();
        if (user == null) return;

        // משימת Analysis
        var analysisTask = new BaseTask
        {
            TaskType = "Analysis",
            Description = "ניתוח דרישות",
            AssignedToUserId = user.Id,
            CustomDataJson = JsonSerializer.Serialize(new
            {
                stakeholders = new[] { "PM", "CTO", "Designer" },
                estimatedDays = 5,
                deliverables = "דוקומנטציה, דיאגרמות"
            })
        };

        // משימת Development
        var devTask = new BaseTask
        {
            TaskType = "Development",
            Description = "פיתוח תכונה חדשה",
            AssignedToUserId = user.Id,
            CustomDataJson = JsonSerializer.Serialize(new
            {
                language = "C#",
                framework = ".NET 8",
                estimatedHours = 40,
                complexity = "High"
            })
        };

        // משימת Testing
        var testTask = new BaseTask
        {
            TaskType = "Testing",
            Description = "בדיקת תכונה",
            AssignedToUserId = user.Id,
            CustomDataJson = JsonSerializer.Serialize(new
            {
                testCases = 50,
                coverage = "95%",
                tools = new[] { "xUnit", "NUnit" }
            })
        };

        _context.Tasks.AddRange(analysisTask, devTask, testTask);
        await _context.SaveChangesAsync();
    }

    /* ============================================
       דוגמה 7: קבלת משימות של משתמש מסוים
       ============================================ */

    public async Task GetUserTasksExample(int userId)
    {
        var user = await _context.Users
            .Include(u => u.Tasks)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user != null)
        {
            Console.WriteLine($"משימות של {user.Name}:");
            foreach (var task in user.Tasks)
            {
                Console.WriteLine($"  - {task.Description} ({task.TaskType})");
            }
        }
    }

    /* ============================================
       עזר: המרת סטטוס למחרוזת
       ============================================ */

    private static string GetStatusName(int status) => status switch
    {
        0 => "לא התחילה",
        1 => "בתהליך",
        2 => "הושלמה",
        3 => "ביוטלה",
        _ => "לא ידוע"
    };
}

/* ============================================
   הערות חשובות
   ============================================ 

1. JSON Serialization:
   - השתמש ב-JsonSerializer.Serialize() ליצירת JSON
   - השתמש ב-JsonSerializer.Deserialize<T>() לקריאת JSON

2. CustomDataJson default:
   - ברירת מחדל היא "{}" (JSON ריק)
   - ניתן להכניס כל JSON שהוא

3. Status values:
   - 0: לא התחילה (Not Started)
   - 1: בתהליך (In Progress)
   - 2: הושלמה (Completed)
   - 3: ביוטלה (Cancelled)

4. EF Core JSON:
   - EF Core 8 תומך בעבודה עם JSON columns
   - שדה CustomDataJson מאוחסן כ-nvarchar(max) בבסיס הנתונים
   - ניתן לשאול עם LINQ (לפי הגרסה של SQL Server)

============================================ */

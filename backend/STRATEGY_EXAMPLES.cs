// 📝 דוגמאות לשימוש ב-Strategy Handlers

using DanTaskManager.Domain;
using DanTaskManager.Domain.Handlers;
using System.Text.Json;

/* ============================================
   דוגמה 1: שימוש ישיר ב-Handlers
   ============================================ */

public class StrategyHandlerExamples
{
    private readonly TaskHandlerFactory _factory;

    public StrategyHandlerExamples(TaskHandlerFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// דוגמה: וולידציה של Procurement משימה בסטטוס 2
    /// </summary>
    public void ProcurementStatusTwoExample()
    {
        var handler = _factory.GetHandler("Procurement");
        
        if (handler == null)
        {
            Console.WriteLine("Handler לא קיים עבור Procurement");
            return;
        }

        Console.WriteLine($"Handler: {handler.TaskType}, Final Status: {handler.FinalStatus}");

        // ✅ דוגמה תקינה - 2 מחירים
        var validJson = JsonSerializer.Serialize(new
        {
            prices = new[] { "5000 ₪", "4800 ₪" }
        });

        var result1 = handler.ValidateStatusChange(
            currentDataJson: "{}",
            currentStatus: 1,
            nextStatus: 2,
            newDataJson: validJson);

        Console.WriteLine($"תוצאה: {(result1.IsValid ? "✅ תקין" : "❌ שגיאה")}: {result1.Message}");

        // ❌ דוגמה לא תקינה - רק מחיר אחד
        var invalidJson = JsonSerializer.Serialize(new
        {
            prices = new[] { "5000 ₪" }
        });

        var result2 = handler.ValidateStatusChange(
            currentDataJson: validJson,
            currentStatus: 2,
            nextStatus: 3,
            newDataJson: invalidJson);

        Console.WriteLine($"תוצאה: {(result2.IsValid ? "✅ תקין" : "❌ שגיאה")}: {result2.Message}");
    }

    /* ============================================
       דוגמה 2: וולידציה של Development משימה
       ============================================ */

    public void DevelopmentStatusFlowExample()
    {
        var handler = _factory.GetHandler("Development");
        
        if (handler == null) return;

        Console.WriteLine($"\n=== Development Handler ===");
        Console.WriteLine($"סטטוס סופי: {handler.FinalStatus}");

        // סטטוס 2: טקסט אפיון
        var spec = JsonSerializer.Serialize(new
        {
            specification = "יש לפתח מודול זימון משתמשים עם Swagger UI"
        });

        var res2 = handler.ValidateStatusChange("{}", 1, 2, spec);
        Console.WriteLine($"סטטוס 2: {(res2.IsValid ? "✅" : "❌")} {res2.Message}");

        // סטטוס 3: שם בראנץ'
        var branch = JsonSerializer.Serialize(new
        {
            specification = "יש לפתח מודול זימון משתמשים עם Swagger UI",
            branchName = "feature/user-invitation"
        });

        var res3 = handler.ValidateStatusChange(spec, 2, 3, branch);
        Console.WriteLine($"סטטוס 3: {(res3.IsValid ? "✅" : "❌")} {res3.Message}");

        // סטטוס 4: גרסה
        var version = JsonSerializer.Serialize(new
        {
            specification = "יש לפתח מודול זימון משתמשים עם Swagger UI",
            branchName = "feature/user-invitation",
            versionNumber = "1.2.0"
        });

        var res4 = handler.ValidateStatusChange(branch, 3, 4, version);
        Console.WriteLine($"סטטוס 4: {(res4.IsValid ? "✅" : "❌")} {res4.Message}");
    }

    /* ============================================
       דוגמה 3: קבלת סטטוס סופי
       ============================================ */

    public void GetFinalStatusExample()
    {
        Console.WriteLine($"\n=== Final Status Info ===");
        Console.WriteLine($"Procurement: {_factory.GetHandler("Procurement")?.FinalStatus}");
        Console.WriteLine($"Development: {_factory.GetHandler("Development")?.FinalStatus}");
        Console.WriteLine($"Unknown: {_factory.GetHandler("Unknown")?.FinalStatus ?? -1} (not found)");
    }

    /* ============================================
       דוגמה 4: הרחבה קלה - הוספת Handler חדש
       ============================================ */

    public void ShowHowToExtendExample()
    {
        Console.WriteLine($"\n=== כיצד להוסיף Handler חדש ===");
        Console.WriteLine(@"
1. יצור מחלקה חדשה שמממשת ITaskHandler:
   public class TestingTaskHandler : ITaskHandler
   {
       public string TaskType => ""Testing"";
       public int FinalStatus => 3;
       
       public ValidationResult ValidateStatusChange(...)
       {
           // לוגיקה וולידציה
       }
   }

2. הוסף הרשמה ב-Program.cs:
   builder.Services.AddTransient<ITaskHandler, TestingTaskHandler>();

3. זהו! TaskHandlerFactory ילקח אותו באופן אוטומטי!");
    }

    /* ============================================
       דוגמה 5: שימוש בפקטורי
       ============================================ */

    public void FactoryPatternExample()
    {
        Console.WriteLine($"\n=== Factory Pattern ===");

        // קבלת כל ה-Handler המרשומים
        var registeredTypes = _factory.GetRegisteredTaskTypes();
        Console.WriteLine("Handlers המרשומים:");
        foreach (var type in registeredTypes)
        {
            var handler = _factory.GetHandler(type);
            Console.WriteLine($"  - {type} (Final Status: {handler?.FinalStatus})");
        }

        // בדיקה האם יש Handler
        if (_factory.HasHandler("Procurement"))
        {
            Console.WriteLine("\nProcurement Handler קיים ✓");
        }

        // קבלת Handler לא קיים
        var unknown = _factory.GetHandler("UnknownType");
        Console.WriteLine($"UnknownType Handler: {(unknown == null ? "לא קיים" : "קיים")}");
    }
}

/* ============================================
   REST API דוגמאות - Using Endpoints
   ============================================ 

1. שינוי סטטוס של Procurement משימה:
   
   POST /api/tasks/1/change-status
   {
     "nextStatus": 2,
     "newDataJson": "{\"prices\": [\"5000 ₪\", \"4800 ₪\"]}"
   }
   
   Response ✅:
   {
     "success": true,
     "message": "סטטוס עודכן בהצלחה מ-1 ל-2",
     "task": { ... }
   }
   
   Response ❌ (כמות מחירים לא תקינה):
   {
     "error": "'prices' חייב להכיל בדיוק 2 מחרוזות, נמצאו 1"
   }

2. שינוי סטטוס של Development משימה:
   
   POST /api/tasks/5/change-status
   {
     "nextStatus": 2,
     "newDataJson": "{\"specification\": \"יש לפתח API לניהול משתמשים עם authentication\"}"
   }

3. קבלת משימה (כולל JSON data):
   
   GET /api/tasks/1
   
   Response:
   {
     "id": 1,
     "taskType": "Procurement",
     "currentStatus": 2,
     "customDataJson": "{\"prices\": [\"5000 ₪\", \"4800 ₪\"]}",
     ...
   }

============================================ */

/* ============================================
   עקרונות Design Patterns שמומשו
   ============================================ 

1. **Strategy Pattern**:
   - ITaskHandler = Strategy interface
   - ProcurementTaskHandler, DevelopmentTaskHandler = Concrete Strategies
   - כל strategy מטפל בוולידציה שונה

2. **Factory Pattern**:
   - TaskHandlerFactory = Concrete Factory
   - מנהל את ה-Handlers ויוצר את המתאים לפי סוג

3. **Dependency Injection**:
   - IEnumerable<ITaskHandler> מזריקה את כל ה-Handlers
   - TaskHandlerFactory מזריקה את עצמה

4. **Open/Closed Principle**:
   - פתוח להרחבה: הוספת Handler חדש לא דורש שינוי קוד קיים
   - סגור לשינוי: TaskHandlerFactory לא משתנה כשמוסיפים Handlers

============================================ */

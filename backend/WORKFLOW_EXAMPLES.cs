// 📝 דוגמאות - TaskWorkflowService וREST API

using DanTaskManager.Domain;
using DanTaskManager.Services;
using System.Text.Json;

/* ============================================
   דוגמה 1: Workflow עם תנועה קדימה בלבד
   ============================================ */

public class WorkflowScenarios
{
    private readonly ITaskWorkflowService _workflowService;

    public WorkflowScenarios(ITaskWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    /// <summary>
    /// דוגמה: Procurement תרחיש מלא
    /// </summary>
    public async Task ProcurementFullWorkflowExample()
    {
        Console.WriteLine("\n=== Procurement Workflow Example ===\n");

        // 1️⃣ משימה נוצרה בסטטוס 0 (התחלה)
        Console.WriteLine("1. משימה נוצרה בסטטוס 0 (התחלה)");

        // 2️⃣ תנועה קדימה ל-Status 1
        Console.WriteLine("2. תנועה קדימה: 0 → 1");
        var result1 = await _workflowService.ChangeStatusAsync(
            taskId: 1,
            newStatus: 1,
            nextAssignedToUserId: 1,
            newDataJson: "{}");
        Console.WriteLine($"   Result: {(result1.Success ? "✅" : "❌")} - {result1.Message}");

        // 3️⃣ תנועה קדימה ל-Status 2 עם מחירים
        Console.WriteLine("3. תנועה קדימה: 1 → 2 (עם מחירים)");
        var prices2 = JsonSerializer.Serialize(new { prices = new[] { "5000 ₪", "4800 ₪" } });
        var result2 = await _workflowService.ChangeStatusAsync(
            taskId: 1,
            newStatus: 2,
            nextAssignedToUserId: 1,
            newDataJson: prices2);
        Console.WriteLine($"   Result: {(result2.Success ? "✅" : "❌")} - {result2.Message}");

        // 4️⃣ ❌ ניסיון דילוג: 2 → 4 (לא מותר)
        Console.WriteLine("4. ❌ ניסיון דילוג: 2 → 4");
        var resultFail = await _workflowService.ChangeStatusAsync(
            taskId: 1,
            newStatus: 4,
            nextAssignedToUserId: 1,
            newDataJson: "{}");
        Console.WriteLine($"   Result: {(resultFail.Success ? "✅" : "❌")} - {resultFail.Message}");

        // 5️⃣ תנועה קדימה ל-Status 3 עם קבלה
        Console.WriteLine("5. תנועה קדימה: 2 → 3 (סטטוס סופי)");
        var receipt = JsonSerializer.Serialize(new 
        { 
            prices = new[] { "5000 ₪", "4800 ₪" },
            receipt = "REC-2026-0525-001"
        });
        var result3 = await _workflowService.ChangeStatusAsync(
            taskId: 1,
            newStatus: 3,
            nextAssignedToUserId: 1,
            newDataJson: receipt);
        Console.WriteLine($"   Result: {(result3.Success ? "✅" : "❌")} - {result3.Message}");

        // 6️⃣ ❌ ניסיון להעבור את FinalStatus
        Console.WriteLine("6. ❌ ניסיון להעבור את FinalStatus: 3 → 4");
        var resultBeyond = await _workflowService.ChangeStatusAsync(
            taskId: 1,
            newStatus: 4,
            nextAssignedToUserId: 1,
            newDataJson: "{}");
        Console.WriteLine($"   Result: {(resultBeyond.Success ? "✅" : "❌")} - {resultBeyond.Message}");

        // 7️⃣ סגירת משימה (Status 99)
        Console.WriteLine("7. סגירת משימה (Status 99)");
        var closeResult = await _workflowService.CloseTaskAsync(
            taskId: 1,
            finalNotes: "משימה הושלמה בהצלחה");
        Console.WriteLine($"   Result: {(closeResult.Success ? "✅" : "❌")} - {closeResult.Message}");

        // 8️⃣ ❌ ניסיון לשנות משימה סגורה
        Console.WriteLine("8. ❌ ניסיון לשנות משימה סגורה");
        var resultClosed = await _workflowService.ChangeStatusAsync(
            taskId: 1,
            newStatus: 2,
            nextAssignedToUserId: 1,
            newDataJson: "{}");
        Console.WriteLine($"   Result: {(resultClosed.Success ? "✅" : "❌")} - {resultClosed.Message}");
    }

    /* ============================================
       דוגמה 2: Rollback (תנועה אחורה)
       ============================================ */

    public async Task RollbackExample()
    {
        Console.WriteLine("\n=== Rollback Example ===\n");

        // משימה בסטטוס 3
        Console.WriteLine("משימה במצב: סטטוס 3");

        // ↩️ Rollback: 3 → 2
        Console.WriteLine("Rollback: 3 → 2");
        var result1 = await _workflowService.ChangeStatusAsync(
            taskId: 1,
            newStatus: 2,
            nextAssignedToUserId: 1,
            newDataJson: "{}");
        Console.WriteLine($"Result: {(result1.Success ? "✅" : "❌")} {result1.Message}");

        // ↩️ Rollback: 2 → 1
        Console.WriteLine("Rollback: 2 → 1");
        var result2 = await _workflowService.ChangeStatusAsync(
            taskId: 1,
            newStatus: 1,
            nextAssignedToUserId: 1,
            newDataJson: "{}");
        Console.WriteLine($"Result: {(result2.Success ? "✅" : "❌")} {result2.Message}");

        // ↩️ Rollback: 1 → 0
        Console.WriteLine("Rollback: 1 → 0");
        var result3 = await _workflowService.ChangeStatusAsync(
            taskId: 1,
            newStatus: 1,
            nextAssignedToUserId: 1,
            newDataJson: "{}");
        Console.WriteLine($"Result: {(result3.Success ? "✅" : "❌")} {result3.Message}");
    }

    /* ============================================
       דוגמה 3: Development Workflow
       ============================================ */

    public async Task DevelopmentWorkflowExample()
    {
        Console.WriteLine("\n=== Development Workflow Example ===\n");

        // סטטוס 0 → 1
        Console.WriteLine("1. 0 → 1");
        var res1 = await _workflowService.ChangeStatusAsync(1, 1, 1, "{}");
        Console.WriteLine($"   {(res1.Success ? "✅" : "❌")}");

        // סטטוס 1 → 2 (עם specification)
        Console.WriteLine("2. 1 → 2 (עם specification)");
        var spec = JsonSerializer.Serialize(new 
        { 
            specification = "יש לפתח API לניהול משתמשים עם JWT authentication וSwagger documentation"
        });
        var res2 = await _workflowService.ChangeStatusAsync(1, 2, 1, spec);
        Console.WriteLine($"   {(res2.Success ? "✅" : "❌")} {res2.Message}");

        // סטטוס 2 → 3 (עם branchName)
        Console.WriteLine("3. 2 → 3 (עם branchName)");
        var branch = JsonSerializer.Serialize(new 
        { 
            specification = "...",
            branchName = "feature/user-management-api"
        });
        var res3 = await _workflowService.ChangeStatusAsync(1, 3, 1, branch);
        Console.WriteLine($"   {(res3.Success ? "✅" : "❌")} {res3.Message}");

        // סטטוס 3 → 4 (עם versionNumber)
        Console.WriteLine("4. 3 → 4 (עם versionNumber - סטטוס סופי)");
        var version = JsonSerializer.Serialize(new 
        { 
            specification = "...",
            branchName = "feature/user-management-api",
            versionNumber = "1.0.0"
        });
        var res4 = await _workflowService.ChangeStatusAsync(1, 4, 1, version);
        Console.WriteLine($"   {(res4.Success ? "✅" : "❌")} {res4.Message}");

        // סגירה
        Console.WriteLine("5. סגירה (Status 99)");
        var close = await _workflowService.CloseTaskAsync(1, "משימה הושלמה וLive");
        Console.WriteLine($"   {(close.Success ? "✅" : "❌")} {close.Message}");
    }

    /* ============================================
       דוגמה 4: קבלת משימות משתמש
       ============================================ */

    public async Task GetUserTasksExample()
    {
        Console.WriteLine("\n=== Get User Tasks ===\n");

        var tasks = await _workflowService.GetUserTasksAsync(userId: 1);
        
        Console.WriteLine($"משימות פעילות של משתמש 1:");
        foreach (var task in tasks)
        {
            Console.WriteLine($"  - ID: {task.Id}, Type: {task.TaskType}, Status: {task.CurrentStatus}");
        }
    }

    /* ============================================
       דוגמה 5: מקרה שגיאה - וולידציה Handler
       ============================================ */

    public async Task ValidationFailureExample()
    {
        Console.WriteLine("\n=== Validation Failure Example ===\n");

        // ❌ ניסיון תנועה ל-Status 2 ללא מחירים (Procurement)
        Console.WriteLine("❌ ניסיון תנועה ל-Status 2 ללא מחירים:");
        var invalidData = JsonSerializer.Serialize(new { some = "data" });
        var result = await _workflowService.ChangeStatusAsync(
            taskId: 1,
            newStatus: 2,
            nextAssignedToUserId: 1,
            newDataJson: invalidData);
        
        Console.WriteLine($"Result: {(result.Success ? "✅" : "❌")}");
        Console.WriteLine($"Message: {result.Message}");
    }

    /* ============================================
       דוגמה 6: Invalid Movement
       ============================================ */

    public async Task InvalidMovementExample()
    {
        Console.WriteLine("\n=== Invalid Movement Example ===\n");

        // משימה בסטטוס 1
        // ❌ ניסיון דילוג לסטטוס 3
        Console.WriteLine("❌ ניסיון דילוג: 1 → 3");
        var result = await _workflowService.ChangeStatusAsync(
            taskId: 1,
            newStatus: 3,
            nextAssignedToUserId: 1,
            newDataJson: "{}");
        
        Console.WriteLine($"Result: {(result.Success ? "✅" : "❌")}");
        Console.WriteLine($"Message: {result.Message}");
        // Message: "תנועה קדימה חייבת להיות בדיוק ב-1 סטטוס..."
    }
}

/* ============================================
   REST API Examples - cURL / Postman
   ============================================ */

/*

### 1. Create Task
POST http://localhost:5000/api/tasks
Content-Type: application/json

{
  "taskType": "Procurement",
  "description": "רכישת חומרים לפרויקט",
  "assignedToUserId": 1,
  "customDataJson": "{}"
}


### 2. Forward: 0 → 1
POST http://localhost:5000/api/tasks/1/change-status
Content-Type: application/json

{
  "newStatus": 1,
  "newDataJson": "{}"
}


### 3. Forward: 1 → 2 (with data)
POST http://localhost:5000/api/tasks/1/change-status
Content-Type: application/json

{
  "newStatus": 2,
  "newDataJson": "{\"prices\": [\"5000 ₪\", \"4800 ₪\"]}"
}


### 4. Invalid Jump: 2 → 4 ❌
POST http://localhost:5000/api/tasks/1/change-status
Content-Type: application/json

{
  "newStatus": 4,
  "newDataJson": "{}"
}
Response: 400 Bad Request
{
  "error": "תנועה קדימה חייבת להיות בדיוק ב-1 סטטוס..."
}


### 5. Forward: 2 → 3 (final)
POST http://localhost:5000/api/tasks/1/change-status
Content-Type: application/json

{
  "newStatus": 3,
  "newDataJson": "{\"prices\": [...], \"receipt\": \"REC-001\"}"
}


### 6. Rollback: 3 → 2
POST http://localhost:5000/api/tasks/1/change-status
Content-Type: application/json

{
  "newStatus": 2,
  "newDataJson": "{\"prices\": [\"5500\", \"5200\"]}"
}


### 7. Close Task
POST http://localhost:5000/api/tasks/1/close
Content-Type: application/json

{
  "finalNotes": "משימה הושלמה בהצלחה"
}


### 8. Get User Tasks
GET http://localhost:5000/api/tasks/user/1


### 9. Get Single Task
GET http://localhost:5000/api/tasks/1


### 10. Get All Tasks
GET http://localhost:5000/api/tasks


### 11. Get Tasks by Type
GET http://localhost:5000/api/tasks/byType/Procurement

*/

/* ============================================
   Key Takeaways
   ============================================ 

1. **Forward Movement**: Must be +1
   ✅ 0 → 1
   ✅ 1 → 2
   ❌ 1 → 3
   ❌ 1 → 0 (backward)

2. **Backward Movement**: Any lower status
   ✅ 3 → 2
   ✅ 3 → 1
   ✅ 3 → 0

3. **Final Status**: Cannot exceed (handler-specific)
   ❌ Procurement: Cannot go beyond 3
   ❌ Development: Cannot go beyond 4

4. **Closed Status**: 99
   ❌ Cannot change after closing

5. **Validation**: Handler-specific rules
   ✅ Procurement Status 2: needs prices[]
   ✅ Development Status 2: needs specification

============================================ */

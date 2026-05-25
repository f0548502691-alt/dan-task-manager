# 🎯 Dan Task Manager - Comprehensive Project Guide

## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK
- SQL Server (LocalDB)
- Visual Studio / VS Code

### Setup
```bash
# 1. Restore dependencies
dotnet restore

# 2. Build project
dotnet build

# 3. Create database
dotnet ef migrations add InitialCreate
dotnet ef database update

# 4. Run application
dotnet run

# 5. Open Swagger
http://localhost:5000/swagger
```

---

## 📚 Documentation Index

### 🎓 Understanding the System

| Document | Purpose | Read Time |
|----------|---------|-----------|
| [IMPLEMENTATION_COMPLETE.md](IMPLEMENTATION_COMPLETE.md) | **START HERE** - Complete project overview | 10 min |
| [STRATEGY_PATTERN_DOCS.md](STRATEGY_PATTERN_DOCS.md) | Handler architecture & design pattern | 8 min |
| [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md) | Workflow rules & state machine | 10 min |
| [API_ERROR_CODES.md](API_ERROR_CODES.md) | HTTP codes & error messages | 5 min |
| [BEST_PRACTICES.md](BEST_PRACTICES.md) | Code conventions & patterns | 10 min |

### 💻 Code Examples

| Document | Purpose | Type |
|----------|---------|------|
| [WORKFLOW_EXAMPLES.cs](WORKFLOW_EXAMPLES.cs) | Usage examples & scenarios | C# Code |
| [STRATEGY_EXAMPLES.cs](STRATEGY_EXAMPLES.cs) | Handler implementation examples | C# Code |
| [EXAMPLES.cs](EXAMPLES.cs) | General examples | C# Code |

### ✅ Running Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "ClassName=TaskWorkflowServiceTests"

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

---

## 🏗️ Project Structure

```
dan-task-manager/
├── Domain/                          # Business models
│   ├── AppUser.cs                  # User entity
│   ├── BaseTask.cs                 # Task entity
│   └── Handlers/                   # Strategy pattern
│       ├── ITaskHandler.cs         # Handler interface
│       ├── StatusValidationTaskHandlerBase.cs
│       ├── ProcurementTaskHandler.cs
│       ├── DevelopmentTaskHandler.cs
│       └── TaskHandlerFactory.cs
│
├── Services/                        # Business logic
│   ├── ITaskApplicationService.cs  # Task API use cases
│   ├── TaskApplicationService.cs
│   ├── IUserApplicationService.cs  # User API use cases
│   ├── UserApplicationService.cs
│   ├── TaskWorkflowService.cs      # Includes ITaskWorkflowService
│   ├── ITaskStatusService.cs
│   ├── TaskStatusService.cs
│   └── TaskHandlerRegistrationExtensions.cs
│
├── Controllers/                     # REST API
│   ├── TasksController.cs          # 9 endpoints
│   └── UsersController.cs
│
├── Data/
│   └── ApplicationDbContext.cs     # EF Core DbContext
│
├── Tests/
│   ├── HandlerTests.cs             # Handler unit tests
│   └── WorkflowServiceTests.cs      # Workflow unit tests
│
├── Program.cs                       # DI & App setup
├── appsettings.json               # Configuration
│
└── Documentation/
    ├── IMPLEMENTATION_COMPLETE.md  # ⭐ START HERE
    ├── WORKFLOW_SERVICE_DOCS.md
    ├── STRATEGY_PATTERN_DOCS.md
    ├── API_ERROR_CODES.md
    └── BEST_PRACTICES.md
```

---

## 📊 Key Concepts

### Workflow States
```
Procurement:  0 → 1 → 2 → 3 → 99 (FinalStatus: 3)
Development:  0 → 1 → 2 → 3 → 4 → 99 (FinalStatus: 4)

Rules:
✅ Forward: +1 only
✅ Backward: to any lower status
❌ Jump: not allowed (2→4 invalid)
❌ Closed: Status 99 is immutable
```

### Handler Validation
```csharp
ProcurementTaskHandler:
  Status 2: Requires prices[]
  Status 3: Requires receipt

DevelopmentTaskHandler:
  Status 2: Requires specification
  Status 3: Requires branchName (valid Git format)
  Status 4: Requires versionNumber (SemVer)
```

### REST API Endpoints (9 total)
```
POST   /api/tasks                    Create task
GET    /api/tasks                    Get all tasks
GET    /api/tasks/{id}               Get single task
GET    /api/tasks/byType/{type}      Filter by type
GET    /api/tasks/user/{userId}      User's tasks
POST   /api/tasks/{id}/change-status Change status
POST   /api/tasks/{id}/close         Close task
PUT    /api/tasks/{id}               Update task
DELETE /api/tasks/{id}               Delete task
```

---

## 🔌 API Usage Examples

### 1. Create Task
```bash
curl -X POST http://localhost:5000/api/tasks \
  -H "Content-Type: application/json" \
  -d '{
    "taskType": "Procurement",
    "description": "Buy components",
    "assignedToUserId": 1
  }'
```

### 2. Change Status
```bash
curl -X POST http://localhost:5000/api/tasks/1/change-status \
  -H "Content-Type: application/json" \
  -d '{
    "newStatus": 1,
    "newDataJson": "{}"
  }'
```

### 3. Close Task
```bash
curl -X POST http://localhost:5000/api/tasks/1/close \
  -H "Content-Type: application/json" \
  -d '{"finalNotes": "Completed successfully"}'
```

---

## 🧪 Testing

### Test Coverage
```
✅ Handler validation tests (20+ tests)
✅ Workflow service tests (15+ tests)
✅ Status movement validation
✅ Error handling
✅ Edge cases
```

### Run Tests
```bash
# All tests
dotnet test

# With output
dotnet test --logger "console;verbosity=normal"

# Specific test
dotnet test --filter "HandlerTests"
```

---

## 💡 Development Workflow

### Add New Handler Type

1. **Create handler class**
   ```csharp
   namespace DanTaskManager.Domain.Handlers;

   public class MyTaskHandler : StatusValidationTaskHandlerBase
   {
       public MyTaskHandler()
           : base(new Dictionary<int, Func<string, ValidationResult>>
           {
               [2] = ValidateStatusTwo
           })
       {
       }

       public string TaskType => "MyTask";
       public int FinalStatus => 3;
       
       private static ValidationResult ValidateStatusTwo(string newDataJson)
       {
           // Your validation logic
       }
   }
   ```

2. **No Program.cs change needed**
   - `AddTaskHandlersFromAssembly(typeof(ITaskHandler).Assembly)` auto-discovers concrete handlers in `DanTaskManager.Domain.Handlers`.

3. **Write tests**
   ```csharp
   [Fact]
   public void ValidateStatusChange_ShouldPass()
   {
       // Your test
   }
   ```

### Add New Endpoint

1. **Add method to the application service interface**
   ```csharp
   public interface ITaskApplicationService
   {
       Task<MyResult> MyOperationAsync(..., CancellationToken cancellationToken = default);
   }
   ```

2. **Implement in the application service**
   ```csharp
   public async Task<MyResult> MyOperationAsync(..., CancellationToken cancellationToken = default)
   {
       // Implementation
   }
   ```

3. **Add to controller**
   ```csharp
   [HttpPost("my-operation")]
   public async Task<IActionResult> MyOperation(...)
   {
       // HTTP handling
   }
   ```

---

## 📞 Common Questions

### Q: How do I change a task status?
A: Use `POST /api/tasks/{id}/change-status` with the new status (must be +1 for forward movement).

### Q: What's the difference between close and change status?
A: 
- `change-status` moves between states 0-N
- `close` sets status to 99 (permanent)

### Q: Can I rollback a task?
A: Yes! Use `change-status` with a lower status number (e.g., 3 → 2).

### Q: What's the CustomDataJson field for?
A: Store handler-specific data (prices, receipt, specification, etc.) as JSON.

### Q: How do I add a new task type?
A: Add a concrete handler under `Domain/Handlers` (usually by extending `StatusValidationTaskHandlerBase`). It is auto-registered by `AddTaskHandlersFromAssembly`.

### Q: What if validation fails?
A: Workflow validation returns 400 with `error` and `code: "workflow_validation_failed"`.

---

## 🔍 Troubleshooting

### Issue: "תנועה קדימה חייבת להיות בדיוק ב-1 סטטוס"
**Solution**: Status movements must be sequential. Move 0→1→2, not 0→2.

### Issue: "'prices' must contain exactly 2 strings"
**Solution**: For Procurement Status 2, provide JSON: `{"prices": ["5000", "4800"]}`

### Issue: "משימה לא קיימת"
**Solution**: Verify the task ID exists. Check with GET /api/tasks/{id}.

### Issue: "משימה סגורה"
**Solution**: Closed tasks (Status 99) cannot be changed. Create a new task if needed.

### Issue: Database connection error
**Solution**: Run `dotnet ef database update` to ensure database exists.

---

## 📈 Performance Notes

- ✅ Async/await throughout for better performance
- ✅ Indexed columns (Email, TaskType) for faster queries
- ✅ DI for loose coupling and testability
- ✅ EF Core for efficient database access

---

## 🔐 Security Considerations

- Validate all inputs on the controller level
- Use proper error messages (don't leak sensitive info)
- Implement authentication/authorization (if needed)
- Log all status changes for audit trail
- Use parameterized queries (EF Core does this)

---

## 📊 Code Statistics

```
Files:              20+
Lines of Code:      3000+
Classes:            15+
Tests:              35+
Documentation:      6 guides
```

---

## 🎓 Learning Path

### Beginner
1. Read [IMPLEMENTATION_COMPLETE.md](IMPLEMENTATION_COMPLETE.md)
2. Run the application locally
3. Test endpoints with Postman/curl
4. Review [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md)

### Intermediate
1. Study [STRATEGY_PATTERN_DOCS.md](STRATEGY_PATTERN_DOCS.md)
2. Read the handler implementations
3. Understand the workflow rules
4. Review existing tests

### Advanced
1. Read [BEST_PRACTICES.md](BEST_PRACTICES.md)
2. Add a new handler type
3. Add a new endpoint
4. Write comprehensive tests

---

## 🚀 Next Steps

### Run Locally
```bash
dotnet restore
dotnet build
dotnet ef database update
dotnet run
# Open http://localhost:5000/swagger
```

### Deploy
```bash
# Build for production
dotnet publish -c Release

# Use the output folder for deployment
```

### Extend
```bash
# Add new handler type
# Add new endpoint
# Implement authentication
# Add caching
# Set up CI/CD
```

---

## 📝 Documentation Files

| File | Description |
|------|-------------|
| **IMPLEMENTATION_COMPLETE.md** | Project overview & completion status |
| **WORKFLOW_SERVICE_DOCS.md** | Workflow rules & REST API |
| **STRATEGY_PATTERN_DOCS.md** | Handler architecture |
| **API_ERROR_CODES.md** | HTTP codes & error messages |
| **BEST_PRACTICES.md** | Code conventions & patterns |
| **WORKFLOW_EXAMPLES.cs** | Usage examples |
| **STRATEGY_EXAMPLES.cs** | Handler examples |

---

## ✨ Key Features

✅ Extensible handler system (Strategy pattern)  
✅ Flexible data storage (CustomDataJson)  
✅ Strict workflow rules  
✅ 9 REST API endpoints  
✅ 35+ unit tests  
✅ Comprehensive documentation  
✅ Production-ready code  
✅ DI/IoC integrated  

---

## 🎉 You're Ready!

This is a **complete, production-ready task management system** built with:
- **.NET 8** & **EF Core 8**
- **Strategy & Factory patterns**
- **Dependency Injection**
- **Unit Tests & Documentation**

Start with [IMPLEMENTATION_COMPLETE.md](IMPLEMENTATION_COMPLETE.md) for a full overview.

**Happy coding! 🚀**

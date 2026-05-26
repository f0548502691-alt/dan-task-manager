# ⚡ 5-Minute Quick Start - Dan Task Manager

**Time**: ~5 minutes  
**Result**: Running API with Swagger

---

## 🚀 1. Setup (2 minutes)

```bash
# Navigate to project
cd c:\Users\User\project\dan-task-manager

# Restore packages
dotnet restore

# Build
dotnet build
```

---

## 📊 2. Database (1 minute)

```bash
# Create database
dotnet ef database update
```

(If needed first time: `dotnet ef migrations add InitialCreate`)

---

## ▶️ 3. Run Application (1 minute)

```bash
# Start server
dotnet run
```

Wait for:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

---

## 🧪 4. Test API (1 minute)

### Option A: Swagger (Easiest)
```
http://localhost:5000/swagger
```
1. Click "Try it out" on any endpoint
2. Fill in parameters
3. Click "Execute"

### Option B: Postman
```
POST http://localhost:5000/api/tasks
Body:
{
  "taskType": "Procurement",
  "description": "Test task",
  "assignedToUserId": 1
}
```

### Option C: cURL
```bash
curl -X POST http://localhost:5000/api/tasks \
  -H "Content-Type: application/json" \
  -d '{
    "taskType":"Procurement",
    "description":"Test",
    "assignedToUserId":1
  }'
```

---

## ✅ Next Steps

- ✅ App running? → Read [GETTING_STARTED.md](GETTING_STARTED.md)
- ✅ Want API docs? → See [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md)
- ✅ Need help? → Check [API_ERROR_CODES.md](API_ERROR_CODES.md)
- ✅ Want code? → Study [WORKFLOW_EXAMPLES.cs](WORKFLOW_EXAMPLES.cs)

---

## 🎯 Common First Tasks

### Create Task
```bash
POST /api/tasks
{
  "taskType": "Procurement",
  "description": "Buy components",
  "assignedToUserId": 1
}
```

### Change Status
```bash
POST /api/tasks/1/change-status
{
  "newStatus": 2,
  "nextAssignedToUserId": 1,
  "customFields": {
    "prices": ["5000", "4800"]
  }
}
```

### Get User Tasks
```bash
GET /api/tasks/user/1
```

---

## 🆘 Troubleshooting

**Error: "Cannot connect to database"**
```bash
# Fix: Ensure database is created
dotnet ef database update
```

**Error: "Port 5000 already in use"**
```bash
# Either:
# 1. Use different port: dotnet run --urls "http://localhost:5001"
# 2. Kill process using port 5000
```

**Tests failing?**
```bash
# Run tests
dotnet test
```

---

## 📚 Quick Navigation

| Want | Link |
|------|------|
| Setup help | [GETTING_STARTED.md](GETTING_STARTED.md) |
| API endpoints | [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md) |
| Code examples | [WORKFLOW_EXAMPLES.cs](WORKFLOW_EXAMPLES.cs) |
| All docs | [DOCUMENTATION_INDEX.md](DOCUMENTATION_INDEX.md) |

---

## 🎉 You're Done!

Your API is ready. Start using it with:
- **Swagger**: http://localhost:5000/swagger
- **Postman**: Import endpoints
- **cURL**: Use command line

**Happy testing! 🚀**

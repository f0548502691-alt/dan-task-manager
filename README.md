# DanTaskManager

Full-stack task-management sample with:

- **Backend:** ASP.NET Core 8 + EF Core + SQL Server (`/backend`)
- **Frontend:** Angular (`/frontend`)

## Prerequisites

- Docker + Docker Compose
- .NET 8 SDK (for local backend development)
- Node.js 20+ and npm

## Quick start (Docker stack)

### 1) Start SQL Server, backend API, and frontend

From the repository root:

```bash
cp .env.example .env
# Optionally edit DANTASKMANAGER_DB_PASSWORD in .env before first run.
# SQL Server requires 8+ chars with mixed character types.
docker compose up -d
```

Docker Compose starts:

- SQL Server on `localhost:1433` by default.
- Backend API on `http://localhost:8080` (Swagger at `/swagger` in Development).
- Angular frontend on `http://localhost:4200`.

`depends_on` only controls container start order. The backend also retries database
initialization on startup, so the stack can tolerate SQL Server taking a little
longer to accept connections.

### 2) Optional: run the frontend locally

For local frontend development outside Docker, keep the backend container running
and start Angular in a second terminal:

```bash
cd frontend
npm install
npm start
```

The Angular dev server runs on `http://localhost:4200` and proxies `/api` requests to `http://localhost:8080` using `frontend/proxy.conf.json`.

## Backend migrations and seed data

The backend uses EF Core migrations. On application startup,
`Program.InitializeDatabase` checks for migrations and runs `Database.Migrate()`;
if no migrations exist, it falls back to `EnsureCreated()`.

The initial migration creates schema and seeds demo users, task types, field
definitions, and sample tasks. The seed rows are authored in
`backend/Data/ApplicationDbContext.SeedData`, while the initial migration applies
them with explicit SQL Server literals (`datetime2`, `bit`, Unicode strings, and
`decimal(18,2)` numeric bounds). Keep those two places aligned when changing
baseline seed data for a fresh database.

Seeded demo users:

- `dan@example.com`
- `ruth@example.com`
- `moshe@example.com`
- `noa@example.com`
- `eitan@example.com`
- `michal@example.com`

To apply migrations manually:

```bash
cd backend
dotnet ef database update
```

If startup fails while applying migrations, inspect the backend logs first. Common
causes are a SQL Server password that fails the container policy, an occupied
host port, or a stale `sqlserver-data` Docker volume from an earlier schema.

## Extensibility approach (adding a new task type)

Task-type behavior is intentionally pluggable:

1. **Metadata-first path (preferred):** add a row to `TaskTypeMetadata` and field rules to `TaskFieldDefinition`. The metadata workflow provider picks it up automatically.
2. **Code-backed path:** implement `IRegisterableTaskHandler` when rules cannot be expressed declaratively. Registration is automatic via assembly scanning in `AddTaskHandlersFromAssembly`.

This lets new task types be introduced without editing existing task-type handlers.

## More details

- Backend setup and database runbook: `backend/docs/QUICKSTART.md`
- Workflow and metadata rules: `backend/docs/WORKFLOW.md`
- Extension deep dive: `backend/docs/EXTENSION_GUIDE.md`

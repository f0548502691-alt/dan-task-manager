# DanTaskManager

Full-stack task-management sample with:

- **Backend:** ASP.NET Core 8 + EF Core + SQL Server (`/backend`)
- **Frontend:** Angular (`/frontend`)

## Prerequisites

- Docker + Docker Compose
- .NET 8 SDK (for local backend development)
- Node.js 20+ and npm

## Quick start (run both server and client)

### 1) Start SQL Server + backend API

From the repository root:

```bash
cp .env.example .env
# Edit DB_PASSWORD in .env before first run
docker compose up -d
```

The backend listens on `http://localhost:8080` (Swagger at `/swagger` in Development).

### 2) Start the frontend

In a second terminal:

```bash
cd frontend
npm install
npm start
```

The Angular dev server runs on `http://localhost:4200` and proxies `/api` requests to `http://localhost:8080` using `frontend/proxy.conf.json`.

## Backend migrations and seeded demo users

The backend uses EF Core migrations. The initial migration creates schema + seed data for demo users, task types, field definitions, and sample tasks.

On startup, the API checks for migrations and applies them automatically before serving requests. Manual migration commands are mainly useful when running the backend outside Docker or validating a schema change locally.

Seeded demo users:

- `dan@example.com`
- `ruth@example.com`
- `moshe@example.com`
- `noa@example.com`
- `eitan@example.com`
- `michal@example.com`

To apply migrations manually (optional, usually auto-applied on startup):

```bash
cd backend
dotnet ef database update
```

When changing the EF model, create a new migration from `/backend` and commit both the generated migration file and `Migrations/ApplicationDbContextModelSnapshot.cs`.

The seeded task-type metadata includes `Procurement`, `Development`, and `Marketing`. Workflow status values start at `1`; closed tasks use status `99` and are only reached through the close endpoint.

## Extensibility approach (adding a new task type)

Task-type behavior is intentionally pluggable:

1. **Metadata-first path (preferred):** add a row to `TaskTypeMetadata` and field rules to `TaskFieldDefinition`. The metadata workflow provider picks it up automatically.
2. **Code-backed path:** implement `IRegisterableTaskHandler` when rules cannot be expressed declaratively. Registration is automatic via assembly scanning in `AddTaskHandlersFromAssembly`.

This lets new task types be introduced without editing existing task-type handlers.

## More details

- Backend guide: `backend/README.md`
- Backend quickstart and troubleshooting: `backend/docs/QUICKSTART.md`
- Workflow rules: `backend/docs/WORKFLOW.md`
- Extension deep dive: `backend/docs/EXTENSION_GUIDE.md`

## Common setup pitfalls

- `docker compose up` requires `DB_PASSWORD` in `.env`; SQL Server rejects weak passwords.
- The backend fails fast if `ConnectionStrings:DefaultConnection` is missing.
- The Angular dev server expects the API on `http://localhost:8080` through `frontend/proxy.conf.json`.

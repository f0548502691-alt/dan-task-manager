# DanTaskManager

Full-stack task-management sample with:

- **Backend:** ASP.NET Core 8 + EF Core + SQL Server (`/backend`)
- **Frontend:** Angular (`/frontend`)

## Quick start prerequisites

- Docker + Docker Compose

## Quick start with Docker (run the full app)

From the repository root:

```bash
cp .env.example .env
# Optionally edit DANTASKMANAGER_DB_PASSWORD in .env before first run.
# SQL Server requires 8+ chars with mixed character types.
docker compose up -d
```

Docker Compose starts SQL Server, the backend API, and the frontend. No separate frontend command or local Node.js installation is required for this path.

The backend listens on `http://localhost:8080` (Swagger at `/swagger` in Development).
The frontend runs on `http://localhost:4200` and proxies `/api` requests to the backend service using `frontend/proxy.docker.conf.json`.

## Local development outside Docker

- .NET 8 SDK for running the backend locally
- Node.js 20+ and npm for running the frontend locally

## Local frontend development outside Docker

If you want to run the Angular dev server directly on your machine, install Node.js 20+ and npm, then run:

```bash
cd frontend
npm install
npm start
```

This uses `frontend/proxy.conf.json` to proxy `/api` requests to `http://localhost:8080`.

## Backend migrations and seeded demo users

The backend uses EF Core migrations. The initial migration creates schema + seed data for demo users, task types, field definitions, and sample tasks.

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

## Extensibility approach (adding a new task type)

Task-type behavior is intentionally pluggable:

1. **Metadata-first path (preferred):** add a row to `TaskTypeMetadata` and field rules to `TaskFieldDefinition`. The metadata workflow provider picks it up automatically.
2. **Code-backed path:** implement `IRegisterableTaskHandler` when rules cannot be expressed declaratively. Registration is automatic via assembly scanning in `AddTaskHandlersFromAssembly`.

This lets new task types be introduced without editing existing task-type handlers.

## More details

- Detailed setup and troubleshooting runbook: `backend/docs/QUICKSTART.md`
- Extension deep dive: `backend/docs/EXTENSION_GUIDE.md`

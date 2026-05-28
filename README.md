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
# Optionally edit DANTASKMANAGER_DB_PASSWORD in .env before first run.
# SQL Server requires 8+ chars with mixed character types.
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

On application startup, `backend/Program.cs` initializes the database before the
HTTP pipeline starts:

- if EF migrations exist, it runs `Database.Migrate()`;
- otherwise, it falls back to `Database.EnsureCreated()`;
- it tries initialization up to 30 times with a 2-second delay between failures,
  which gives the SQL Server container time to finish accepting connections after
  `docker compose up`.

Compose uses `DANTASKMANAGER_DB_PASSWORD` from `.env` for both the SQL Server
container and the backend connection string. The value must satisfy SQL Server's
password policy. `depends_on` only controls container start order, so check the
backend logs if startup still fails after the retry window. If you change the
password after the `sqlserver-data` volume already exists, keep using the
existing password or recreate the volume.

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

- Extension deep dive: `backend/docs/EXTENSION_GUIDE.md`

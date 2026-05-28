# DanTaskManager

Full-stack task-management sample with:

- **Backend:** ASP.NET Core 8 + EF Core + SQL Server (`/backend`)
- **Frontend:** Angular (`/frontend`)

## Prerequisites

- Docker + Docker Compose
- .NET 8 SDK (for local backend development)
- Node.js 20+ and npm

## Quick start (Docker Compose)

From the repository root:

```bash
cp .env.example .env
# Optionally edit DANTASKMANAGER_DB_PASSWORD in .env before first run.
# SQL Server requires 8+ chars with mixed character types.
docker compose up --build
```

Compose starts SQL Server, the ASP.NET Core API, and the Angular dev server:

- Frontend: `http://localhost:4200`
- Backend API: `http://localhost:8080`
- Swagger UI: `http://localhost:8080/swagger` when `ASPNETCORE_ENVIRONMENT=Development`
- SQL Server: `localhost:${DB_PORT:-1433}` from the host, persisted in the `sqlserver-data` Docker volume

### Docker startup contract

- `docker-compose.yml` injects `ConnectionStrings__DefaultConnection` into the backend as `Server=db,...`; inside Compose, the database host is the `db` service name, not `localhost`.
- `DANTASKMANAGER_DB_PASSWORD` is shared by the SQL Server container and the backend connection string. If you change it after the `sqlserver-data` volume already exists, recreate the volume or keep using the old password.
- `DB_PORT` is used both for the host port mapping and the backend container connection string. Keep the default `1433` unless the Compose SQL Server listener is changed at the same time.
- `ASPNETCORE_URLS` defaults to `http://+:8080` so Kestrel listens on the container interface exposed by Compose. If you override it, keep it compatible with the `8080:8080` port mapping and the frontend proxy.
- The frontend container runs `npm run start:docker`, which uses `frontend/proxy.docker.conf.json` to proxy `/api` to `http://backend:8080`.

## Local developer workflow

To run the backend locally against the Compose database:

```bash
docker compose up -d db
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=DanTaskManager;User Id=sa;Password=Your_strong_Password123;Encrypt=False;TrustServerCertificate=True;"
dotnet run --project backend
```

To run the frontend locally:

```bash
cd frontend
npm install
npm start
```

The local Angular dev server also runs on `http://localhost:4200`, but it uses `frontend/proxy.conf.json` to proxy `/api` requests to `http://localhost:8080`.

## Backend startup and seeded demo data

`backend/Program.cs` requires a non-empty `ConnectionStrings__DefaultConnection` at startup. The Docker path provides it automatically; local `dotnet run` must use an environment variable or `backend/appsettings.Development.json`.

On startup, the backend applies EF Core migrations when migration files exist; otherwise it falls back to `EnsureCreated()`. The initial migration creates schema and seed data for demo users, task types, field definitions, and sample tasks.

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

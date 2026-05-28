# Backend quickstart

Use this guide when changing API startup, Docker Compose, or local database
configuration. The repository root `README.md` contains the shortest path for
running the full stack.

## Full-stack Docker startup

From the repository root:

```bash
cp .env.example .env
# Optional before the first run: edit DANTASKMANAGER_DB_PASSWORD in .env.
docker compose up --build
```

Services exposed by Compose:

| Service | URL from host | Source |
|---------|---------------|--------|
| Angular frontend | `http://localhost:4200` | `frontend/Dockerfile`, `npm run start:docker` |
| Backend API | `http://localhost:8080` | `backend/Dockerfile`, `backend/Program.cs` |
| Swagger UI | `http://localhost:8080/swagger` | Development environment only |
| SQL Server | `localhost:${DB_PORT:-1433}` | `mcr.microsoft.com/mssql/server:2022-latest` |

Compose injects the backend connection string directly:

```text
Server=db,${DB_PORT:-1433};Database=${DB_NAME:-DanTaskManager};User Id=${DB_USER:-sa};Password=${DANTASKMANAGER_DB_PASSWORD:-Your_strong_Password123};Encrypt=False;TrustServerCertificate=True;
```

Inside the Docker network, `db` is the SQL Server hostname. Do not use
`localhost` for container-to-container database traffic.

## Local backend against the Compose database

Start only SQL Server in Docker:

```bash
docker compose up -d db
```

Run the API locally with an explicit connection string:

```bash
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=DanTaskManager;User Id=sa;Password=Your_strong_Password123;Encrypt=False;TrustServerCertificate=True;"
dotnet run --project backend
```

Alternatively, put the same value in `backend/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=DanTaskManager;User Id=sa;Password=Your_strong_Password123;Encrypt=False;TrustServerCertificate=True;"
  }
}
```

Keep local-only settings out of source control.

## Startup behavior verified in source

- `backend/Program.cs` throws on startup when `ConnectionStrings__DefaultConnection`
  is empty. Docker Compose provides it automatically; local runs must configure it.
- `ASPNETCORE_URLS` defaults to `http://+:8080`, matching the backend container's
  `8080:8080` port mapping and the frontend Docker proxy.
- The backend applies EF Core migrations when migration files exist; otherwise it
  falls back to `EnsureCreated()`.
- Seed data is defined in `Data/ApplicationDbContext.cs` for demo users, task types,
  field definitions, and sample tasks.
- `JsonIndexBootstrapper` runs as a hosted service and creates SQL Server computed
  columns/indexes for JSON fields marked `IsIndexed`.

## Environment variables used by Compose

| Variable | Default | Notes |
|----------|---------|-------|
| `DANTASKMANAGER_DB_PASSWORD` | `Your_strong_Password123` | Used by SQL Server and the backend connection string. Must satisfy SQL Server password rules. |
| `DB_PORT` | `1433` | Used by both the host port mapping and the backend container connection string. Keep `1433` unless the SQL Server listener inside Compose is changed too. |
| `DB_NAME` | `DanTaskManager` | Database name in the backend connection string. |
| `DB_USER` | `sa` | SQL login used by the backend. |
| `ASPNETCORE_ENVIRONMENT` | `Development` | Enables Swagger UI. |
| `ASPNETCORE_URLS` | `http://+:8080` | Kestrel bind address inside the backend container. |

## Troubleshooting

| Symptom | Check |
|---------|-------|
| Backend exits with `Connection string 'DefaultConnection' is not configured` | For local `dotnet run`, set `ConnectionStrings__DefaultConnection` or create `backend/appsettings.Development.json`. For Compose, verify the backend service still defines `ConnectionStrings__DefaultConnection`. |
| SQL Server rejects the password | Use at least 8 characters with mixed character types before the first container start. |
| Login fails after changing `DANTASKMANAGER_DB_PASSWORD` | The `sqlserver-data` volume keeps the original SQL Server password. Keep the old password or recreate the volume with `docker compose down -v` when data loss is acceptable. |
| Backend cannot connect after changing `DB_PORT` | The current Compose file also uses `DB_PORT` inside the backend connection string. SQL Server still listens on `1433` in the Docker network unless the container listener is changed separately. |
| Frontend container cannot reach the API | The Docker proxy must target `http://backend:8080`; `localhost` would point back to the frontend container. |
| Local frontend cannot reach the API | `frontend/proxy.conf.json` expects a local backend at `http://localhost:8080`. |

## Related docs

- Root setup guide: `../../README.md`
- Workflow rules: `WORKFLOW.md`
- Extension guide: `EXTENSION_GUIDE.md`
- API errors: `API_ERROR_CODES.md`

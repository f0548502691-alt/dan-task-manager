# Quick start

This runbook covers the two supported ways to run DanTaskManager from a fresh
checkout:

- **Docker Compose:** full stack with SQL Server, backend API, and Angular client.
- **Local development:** backend and/or frontend started directly on the host.

## Full stack with Docker Compose

Prerequisite: Docker with Docker Compose support.

From the repository root:

```bash
cp .env.example .env
# Optional before the first run: edit DANTASKMANAGER_DB_PASSWORD in .env.
# SQL Server requires 8+ characters with mixed character types.
docker compose up -d
```

Compose starts three services:

| Service | Source | Host port | Notes |
|---------|--------|-----------|-------|
| `db` | `mcr.microsoft.com/mssql/server:2022-latest` | `1433` by default | Password comes from `DANTASKMANAGER_DB_PASSWORD`. Data is stored in the `sqlserver-data` volume. |
| `backend` | `backend/Dockerfile` | `8080` | ASP.NET Core API using `ConnectionStrings__DefaultConnection=Server=db,...`. Swagger is available at `http://localhost:8080/swagger` when `ASPNETCORE_ENVIRONMENT=Development`. |
| `frontend` | `frontend/Dockerfile` | `4200` | Runs `npm run start:docker`, which serves Angular on `0.0.0.0:4200`. |

Open:

- Frontend: `http://localhost:4200`
- Backend API: `http://localhost:8080`
- Swagger UI: `http://localhost:8080/swagger` in Development

The Docker frontend uses `frontend/proxy.docker.conf.json`:

```json
{
  "/api": {
    "target": "http://backend:8080",
    "secure": false,
    "changeOrigin": true
  }
}
```

Use the Compose service name (`backend`) inside containers. `localhost` from the
frontend container would point back to the frontend container, not the API.

### Database startup and seed data

`backend/Program.cs` calls `InitializeDatabase(app)` at startup. It retries database
initialization up to 30 times with a two-second delay, then applies EF migrations
when migrations exist. The initial migration creates schema and seed data from
`Data/ApplicationDbContext.SeedData`, including:

- Demo users: `dan@example.com`, `ruth@example.com`, `moshe@example.com`,
  `noa@example.com`, `eitan@example.com`, `michal@example.com`
- Metadata-backed task types: `Procurement`, `Development`, and `Marketing`
- Field rules and sample tasks

If you change the database password after the SQL Server volume already exists,
recreate the volume or restore the old password. SQL Server stores the initial SA
password in the persisted volume.

### Common Compose commands

```bash
# Follow logs from all services
docker compose logs -f

# Rebuild after changing Dockerfiles, package manifests, or published backend code
docker compose up -d --build

# Stop containers but keep the SQL Server data volume
docker compose down

# Stop containers and remove the SQL Server data volume for a clean seed
docker compose down -v
```

The Compose setup does not mount source directories into the containers. Rebuild
the affected service after source changes that need to run inside Docker.

## Local development outside Docker

Install the tools for the part of the stack you want to run locally:

- .NET 8 SDK for the backend
- Node.js 20+ and npm for the frontend
- SQL Server reachable from the backend

Configure the backend connection string with an environment variable:

```bash
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=DanTaskManager;User Id=sa;Password=Your_strong_Password123;Encrypt=False;TrustServerCertificate=True;"
```

Then run the backend:

```bash
cd backend
dotnet restore
dotnet run
```

Run the frontend in a second terminal:

```bash
cd frontend
npm install
npm start
```

The local Angular script uses `frontend/proxy.conf.json`, which sends `/api`
requests to `http://localhost:8080`.

## Smoke-test the running app

```bash
curl http://localhost:8080/api/task-types
curl "http://localhost:8080/api/tasks?pageNumber=1&pageSize=10"
```

Create a task:

```bash
curl -X POST http://localhost:8080/api/tasks \
  -H "Content-Type: application/json" \
  -d '{
    "taskType": "Procurement",
    "description": "Order laptops",
    "assignedToUserId": 1,
    "customFields": {}
  }'
```

Move a Procurement task from created status `1` to status `2`:

```bash
curl -X POST http://localhost:8080/api/tasks/1/change-status \
  -H "Content-Type: application/json" \
  -d '{
    "newStatus": 2,
    "nextAssignedToUserId": 2,
    "customFields": {
      "prices": ["1200", "1180"]
    }
  }'
```

Workflow status constants live in `Domain/WorkflowConstants.cs`:

- `CreatedStatus = 1`
- `ClosedStatus = 99`

Closing a task requires `POST /api/tasks/{id}/close` and only succeeds from that
task type's final status. See [WORKFLOW.md](WORKFLOW.md) for the full workflow
rules.

## Troubleshooting

| Symptom | What to check |
|---------|---------------|
| Backend logs repeat database initialization failures | The SQL Server container may still be starting. The backend retries automatically; inspect `docker compose logs db backend` if it never recovers. |
| SQL login fails after editing `.env` | The `sqlserver-data` volume may contain an older SA password. Revert the password or remove the volume for a fresh database. |
| Frontend loads but API calls fail in Docker | Confirm the Docker proxy target is `http://backend:8080`, not `localhost`. |
| Frontend cannot reach a locally run backend | Use `npm start` so Angular uses `proxy.conf.json` and targets `http://localhost:8080`. |
| Swagger is missing | Swagger only runs when `ASPNETCORE_ENVIRONMENT=Development`. |

## Related docs

- [WORKFLOW.md](WORKFLOW.md) - status movement and task-type validation rules
- [EXTENSION_GUIDE.md](EXTENSION_GUIDE.md) - adding metadata-backed or code-backed task types
- [API_ERROR_CODES.md](API_ERROR_CODES.md) - API error response reference

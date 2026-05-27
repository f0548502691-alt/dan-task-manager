# Quickstart

Use this guide to run DanTaskManager locally and verify that the frontend, backend,
and database are wired together correctly.

## Option A: full stack with Docker Compose

From the repository root:

```bash
cp .env.example .env
# Edit .env and set a strong DB_PASSWORD.
docker compose up --build
```

Compose starts:

| Service | Source | Host URL/port | Notes |
|---------|--------|---------------|-------|
| `frontend` | `frontend/Dockerfile` | http://localhost:4200 | Angular dev server running `npm run start:docker`. |
| `backend` | `backend/Dockerfile` | http://localhost:8080 | .NET API; Swagger is enabled in Development. |
| `db` | SQL Server 2022 image | `localhost:${DB_PORT:-1433}` | Data is stored in the `sqlserver-data` volume. |

The frontend uses relative `/api` calls. In Docker, `frontend/proxy.docker.conf.json`
routes those calls to `http://backend:8080`, where `backend` is the Compose service
name.

## Option B: backend and frontend on the host

Start SQL Server yourself, or use only the Compose database service. Then configure the
backend connection string with either an environment variable:

```bash
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=DanTaskManager;User Id=sa;Password=YOUR_PASSWORD;Encrypt=False;TrustServerCertificate=True;"
```

or a gitignored `backend/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=DanTaskManager;User Id=sa;Password=YOUR_PASSWORD;Encrypt=False;TrustServerCertificate=True;"
  }
}
```

Run the API:

```bash
cd backend
dotnet restore
dotnet run
```

In another terminal, run the frontend:

```bash
cd frontend
npm ci
npm start
```

`npm start` uses `frontend/proxy.conf.json`, which targets `http://localhost:8080`.

## Smoke checks

- Frontend: open http://localhost:4200 and load a seeded user ID.
- API metadata: `GET http://localhost:8080/api/task-types`
- Swagger: http://localhost:8080/swagger when the backend environment is Development.
- Created tasks start at status `1`; closed tasks use status `99`.

## Troubleshooting

| Symptom | Check |
|---------|-------|
| Compose fails with `DB_PASSWORD` missing | Copy `.env.example` to `.env` and set `DB_PASSWORD`, or export it in the shell. |
| SQL Server exits during startup | Use a strong SA password that satisfies the SQL Server image requirements. |
| Frontend loads but API calls fail in Docker | Keep `proxy.docker.conf.json` pointed at `http://backend:8080`; `localhost` inside the container is not the backend. |
| Frontend cannot be reached on `localhost:4200` | `start:docker` must keep `--host 0.0.0.0` so Docker can publish the dev server port. |
| First browser load shows transient API errors | `depends_on` orders containers but does not wait for the API and database to be ready. Wait for backend startup to finish and refresh. |
| Frontend file changes are missing in Compose | The Docker image copies source during build and does not mount a live volume. Rebuild the frontend service or use local `npm start`. |

## Related docs

- `../../frontend/README.md` - Angular runtime, Docker proxy behavior, and common frontend pitfalls.
- `WORKFLOW.md` - workflow rules and status-transition constraints.
- `EXTENSION_GUIDE.md` - adding metadata-backed or code-backed task types.
- `API_ERROR_CODES.md` - API error response catalog.

# DanTaskManager Frontend

Angular standalone workflow board for creating tasks, moving them through schema-driven
statuses, and closing completed work. The client talks to the backend only through
relative `/api` URLs, so the active proxy file decides where API requests go.

## Runtime shape

| Runtime | Entry point | API proxy target | Notes |
|---------|-------------|------------------|-------|
| Local frontend | `npm start` | `proxy.conf.json` -> `http://localhost:8080` | Use when the backend is running on the host. |
| Docker Compose | `npm run start:docker` | `proxy.docker.conf.json` -> `http://backend:8080` | Used by `frontend/Dockerfile`; `backend` is the Compose service name. |

The Docker image is a development-server image, not a production static-assets image:

- Base image: `node:22-alpine`
- Install step: `npm ci`
- Exposed port: `4200`
- Command: `npm run start:docker`

`start:docker` binds Angular to `0.0.0.0`; without that host binding, the port mapping
from the container to `localhost:4200` would not be reachable from the host.

## Run the full stack with Docker Compose

From the repository root:

```bash
cp .env.example .env
# Edit .env and set a strong DB_PASSWORD.
docker compose up --build
```

Then open:

- Frontend: http://localhost:4200
- Backend API/Swagger in development: http://localhost:8080/swagger
- SQL Server: `localhost:${DB_PORT:-1433}`

Compose builds and starts three services:

| Service | Source | Host port | Purpose |
|---------|--------|-----------|---------|
| `frontend` | `frontend/Dockerfile` | `4200` | Angular dev server and `/api` proxy. |
| `backend` | `backend/Dockerfile` | `8080` | .NET API. Reads `ConnectionStrings__DefaultConnection` from Compose. |
| `db` | `mcr.microsoft.com/mssql/server:2022-latest` | `${DB_PORT:-1433}` | SQL Server data store backed by the `sqlserver-data` volume. |

## Local frontend development

Run the backend locally or through Compose so it listens on `localhost:8080`, then:

```bash
cd frontend
npm ci
npm start
```

Use `npm run build` to validate the Angular production build. The current test script is
a placeholder and prints that no frontend test runner is configured yet.

## Common pitfalls

- `DB_PASSWORD` is required by `docker-compose.yml`; Compose exits before building if it
  is missing from `.env` or the shell environment.
- `depends_on` controls startup order, not backend readiness. If the frontend starts
  before the API and database are ready, wait for backend logs to settle and refresh the
  browser.
- Do not point `proxy.docker.conf.json` at `localhost`; inside the frontend container,
  `localhost` is the frontend container itself. Use `http://backend:8080`.
- The Compose frontend image copies source at build time and does not mount a live
  volume. Rebuild the service after changing frontend files, or use local `npm start`
  for iterative UI work.
- Keep browser API calls relative (`/api/...`) in frontend code so local and Docker
  proxy configurations can share the same application code.

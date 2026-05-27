# Quickstart

This guide gets the backend, SQL Server, seeded data, and the Angular client
running from a clean checkout. It reflects the current EF Core migration
workflow: the repository already includes the initial migration, so do not create
a new initial migration during setup.

## Prerequisites

- Docker and Docker Compose for the full-stack path.
- .NET 8 SDK for local backend development and EF Core commands.
- Node.js 20+ and npm for the frontend.

## Full-stack startup with Docker

From the repository root:

```bash
cp .env.example .env
# Edit DB_PASSWORD before first startup.
docker compose up -d --build
```

What starts:

- SQL Server 2022 on `${DB_PORT:-1433}`.
- Backend API on `http://localhost:8080`.
- Swagger UI at `http://localhost:8080/swagger` when
  `ASPNETCORE_ENVIRONMENT=Development`.

The backend reads `ConnectionStrings__DefaultConnection` from
`docker-compose.yml`. At startup, `Program.cs` checks for migrations and runs
`Database.Migrate()` because `backend/Migrations/20260527184500_InitialSchema.cs`
exists. If a future branch removes all migrations, startup falls back to
`EnsureCreated()`.

## Start the Angular client

In a second terminal:

```bash
cd frontend
npm install
npm start
```

The dev server runs at `http://localhost:4200`. Requests under `/api` are proxied
to `http://localhost:8080` by `frontend/proxy.conf.json`.

## Local backend without Docker Compose

Use this path when SQL Server is already available outside Compose.

```bash
cd backend
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=DanTaskManager;User Id=sa;Password=Your_strong_Password123;Encrypt=False;TrustServerCertificate=True;"
dotnet restore
dotnet run
```

Alternatively, create a gitignored `backend/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=DanTaskManager;User Id=sa;Password=Your_strong_Password123;Encrypt=False;TrustServerCertificate=True;"
  }
}
```

The app intentionally fails fast if no `DefaultConnection` is configured.

## Migration operations

The initial migration creates:

- `Users`
- `Tasks`
- `TaskTypes`
- `TaskFieldDefinitions`

Important constraints and indexes:

- `Users.Email` is unique.
- `TaskTypes.Code` is unique.
- `Tasks.CustomDataJson` is `nvarchar(max)`, defaults to `{}`, and has
  `CK_Tasks_CustomDataJson_IsJson`.
- `Tasks.AssignedToUserId` uses restricted deletes.
- `TaskFieldDefinitions` are unique by `(TaskTypeMetadataId, FieldKey)`.

Apply migrations manually when running outside app startup:

```bash
cd backend
dotnet ef database update
```

Create a new migration only after changing the EF model in
`Data/ApplicationDbContext.cs` or the domain entities:

```bash
cd backend
dotnet ef migrations add DescriptiveChangeName
```

Commit both the generated migration and
`Migrations/ApplicationDbContextModelSnapshot.cs`.

## Seeded development data

The migration seeds six demo users:

| Id | Name | Email |
|----|------|-------|
| 1 | Dan Cohen | dan@example.com |
| 2 | Ruth Levi | ruth@example.com |
| 3 | Moshe Avraham | moshe@example.com |
| 4 | Noa Israeli | noa@example.com |
| 5 | Eitan Barak | eitan@example.com |
| 6 | Michal Gal | michal@example.com |

It also seeds metadata-backed task types:

| Task type | Final status | Required fields by status |
|-----------|--------------|---------------------------|
| Procurement | 3 | status 2: `prices` array of exactly 2 strings; status 3: `receipt` string |
| Development | 4 | status 2: `specification` min length 10; status 3: `branchName` matching `valid_git_branch`; status 4: `versionNumber` matching `semantic_version` |
| Marketing | 3 | status 2: `campaignName` min length 3 and `targetAudience` in `B2B`, `B2C`, `Internal`; status 3: `launchDate` matching `YYYY-MM-DD` |

Two sample tasks are inserted at created status `1`: one Procurement task
assigned to user `1`, and one Development task assigned to user `2`.

## Smoke-test the API

List users:

```bash
curl "http://localhost:8080/api/users?page=1&pageSize=20"
```

List active task-type schemas:

```bash
curl http://localhost:8080/api/task-types
```

Create a Marketing task:

```bash
curl -X POST http://localhost:8080/api/tasks \
  -H "Content-Type: application/json" \
  -d '{
    "taskType": "Marketing",
    "description": "Launch spring campaign",
    "assignedToUserId": 1,
    "customFields": {}
  }'
```

Move it from status `1` to `2` using the `id` returned by the create call
(`3` on a fresh seeded database):

```bash
curl -X POST http://localhost:8080/api/tasks/3/change-status \
  -H "Content-Type: application/json" \
  -d '{
    "newStatus": 2,
    "nextAssignedToUserId": 2,
    "customFields": {
      "campaignName": "Spring campaign",
      "targetAudience": "B2B"
    }
  }'
```

The workflow rules are enforced by `TaskWorkflowService`:

- Created status is `1`.
- Closed status is `99` and is reachable only through
  `POST /api/tasks/{id}/close`.
- Forward status movement must be exactly `+1`.
- Backward movement may target any lower status greater than or equal to `1`.
- `customFields` must be a JSON object and satisfy the target status schema.
- `nextAssignedToUserId` must reference an existing user.

List endpoints return a paged object with `items`, `page`, `pageSize`,
`totalCount`, and `totalPages`. Task detail, create, status-change, and close
responses include `customFields`.

## Troubleshooting

| Symptom | Check |
|---------|-------|
| Backend exits with `Connection string 'DefaultConnection' is not configured` | Set `ConnectionStrings__DefaultConnection`, use Docker Compose, or add `appsettings.Development.json`. |
| SQL Server container will not start | Ensure `DB_PASSWORD` in `.env` satisfies SQL Server password complexity and that `${DB_PORT:-1433}` is free. |
| Manual `dotnet ef` command is unavailable | Install the EF tool with `dotnet tool install --global dotnet-ef`, then reopen the shell if needed. |
| Status change returns a workflow validation error | Compare the target status with `backend/docs/WORKFLOW.md` and `GET /api/task-types/{taskType}`. |
| Filtering by an indexed custom field is slow on a new schema | `JsonIndexBootstrapper` creates computed columns and indexes at API startup for scalar fields marked `IsIndexed = true`; restart the backend after changing field metadata. |

## Related docs

- `backend/README.md` - backend architecture and configuration overview.
- `backend/docs/WORKFLOW.md` - workflow movement rules and provider details.
- `backend/docs/EXTENSION_GUIDE.md` - adding metadata-backed or code-backed task types.
- `backend/docs/API_ERROR_CODES.md` - API error-response catalog.

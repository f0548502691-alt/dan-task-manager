# Backend quickstart

This guide covers the backend database and startup path used by both Docker
Compose and local development.

## Source map

| Area | File |
|------|------|
| EF model and seed data | `backend/Data/ApplicationDbContext.cs` |
| Initial SQL Server migration | `backend/Migrations/20260527184500_InitialSchema.cs` |
| Startup migration/retry loop | `backend/Program.cs` |
| Docker database wiring | `docker-compose.yml`, `.env.example` |
| Workflow rules using seeded task metadata | `backend/docs/WORKFLOW.md` |

## Run with Docker Compose

From the repository root:

```bash
cp .env.example .env
# Optional before first run: edit DANTASKMANAGER_DB_PASSWORD in .env.
# SQL Server requires 8+ characters with mixed character types.
docker compose up -d
```

Compose starts:

- `db`: SQL Server 2022, persisted in the `sqlserver-data` volume.
- `backend`: ASP.NET Core API on `http://localhost:8080`.
- `frontend`: Angular dev server on `http://localhost:4200`.

The backend connection string points at `Server=db,<port>` because containers
talk to each other through the Compose network. For local tools running on the
host, use `localhost,<port>` instead.

## Run the backend locally

Start SQL Server first (Docker Compose can run just the database):

```bash
docker compose up -d db
```

Then configure a host-local connection string and run the API:

```bash
export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=DanTaskManager;User Id=sa;Password=Your_strong_Password123;Encrypt=False;TrustServerCertificate=True;"

cd backend
dotnet restore
dotnet run
```

## Database initialization

`Program.InitializeDatabase` is the startup entry point:

1. It creates an application scope and resolves `ApplicationDbContext`.
2. If EF migrations are present, it runs `Database.Migrate()`.
3. If no migrations are present, it falls back to `Database.EnsureCreated()`.
4. It retries up to 30 times with a 2 second delay, which covers SQL Server
   container startup lag.

Manual migration application is also supported:

```bash
cd backend
dotnet ef database update
```

## Seed data

`ApplicationDbContext.SeedData` is the model-level source for baseline data:

- Users: Dan, Ruth, Moshe, Noa, Eitan, and Michal.
- Task types: `Procurement`, `Development`, and metadata-only `Marketing`.
- Field definitions for workflow validation, including indexed fields such as
  `branchName` and `targetAudience`.
- Sample tasks for Procurement and Development at status `1`.

The initial migration applies the same seed rows with hand-written SQL instead
of generated `InsertData` calls. This keeps SQL Server types explicit for mixed
nullable values:

- `DECLARE @SeedTimestamp datetime2 = '2026-05-25T00:00:00'`
- `CAST(1 AS bit)` / `CAST(0 AS bit)` for boolean columns
- `N'...'` for Unicode strings
- `decimal(18,2)` for `TaskFieldDefinition.MinValue` and `MaxValue`

When changing baseline seed data for a fresh database, update both
`ApplicationDbContext.SeedData` and the initial migration SQL. For a database
that may already be deployed, add a new migration rather than editing an applied
migration in place.

## Troubleshooting

| Symptom | Check |
|---------|-------|
| SQL Server container exits quickly | `DANTASKMANAGER_DB_PASSWORD` must satisfy SQL Server policy. Update `.env`, then recreate the container/volume if it was initialized with the old value. |
| Backend logs repeated database initialization failures | Confirm the `db` container is healthy enough to accept TCP connections and that the connection string uses `Server=db,...` inside Compose or `Server=localhost,...` from the host. |
| Migration fails on a fresh database | Inspect `20260527184500_InitialSchema.cs`; seed inserts should keep column order, explicit casts, and null positions aligned with the table definitions. |
| Fresh data does not match seed changes | Remove the local `sqlserver-data` volume or create a new database name; existing databases are updated by migrations, not reseeded from `HasData`. |

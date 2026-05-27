# DanTaskManager — Backend

.NET 8 + EF Core 8 task-management service that separates the **general workflow
rules** (apply to every task) from the **task-type-specific rules** (validation
per type, per status). New task types can be added without modifying existing
code — either declaratively via metadata rows, or by implementing a single
`IRegisterableTaskHandler` interface that DI picks up automatically.

## Quick start

The connection string is no longer hard-coded in `appsettings.json`. Pick one of
three options to supply it:

```bash
# Option A — Docker (uses .env at the repo root)
cp .env.example .env   # then edit DB_* values
docker compose up -d

# Option B — environment variable (local dotnet run)
export ConnectionStrings__DefaultConnection="Server=.;Database=DanTaskManager;Trusted_Connection=true;Encrypt=false;TrustServerCertificate=true;"

# Option C — appsettings.Development.json (gitignored)
#   { "ConnectionStrings": { "DefaultConnection": "..." } }
```

Then:

```bash
cd backend
dotnet restore
dotnet run
```

The app refuses to start with a clear error if no connection string is
configured. Swagger UI is served at `/swagger` in Development.

To run the test suite:

```bash
dotnet test
```

## Project layout

```
backend/
├── Domain/               Entities, value objects, base types, workflow constants
│   └── Handlers/         Strategy interface + base classes for code-backed task rules
├── Application/
│   └── Tasks/            MediatR commands & queries, one folder per use-case
├── Services/             Workflow engine, rule providers, metadata service,
│                         startup conflict validator
├── Contracts/            DTO contracts shared between API and clients
├── Controllers/          HTTP endpoints (Tasks, TaskTypes, Users)
├── Data/                 ApplicationDbContext, seed data, schema bootstrap
├── Middleware/           Global exception → API-error mapping
├── Validation/           FluentValidation validators
├── Tests/                xUnit tests (alongside the project; same assembly)
└── docs/                 Reference documentation
```

## Architecture in one diagram

```
Controller / MediatR handler
        │
        ▼
TaskWorkflowService            ← general rules (forward +1, backward, no jumps,
        │                        closed = immutable, final-status guard, JSON
        │                        payload, assignee existence)
        ▼
ITaskWorkflowRuleProvider[]    ← ordered by Priority; first CanHandle wins
        ├── Metadata provider  ← TaskTypeMetadata + TaskFieldDefinition rows
        └── Handler provider   ← TaskHandlerFactory → IRegisterableTaskHandler
```

A startup `IHostedService` (`TaskTypeConflictValidator`) scans every registered
rule provider and flags any task-type code claimed by more than one source.

## Adding a new task type

Two supported paths, both **without touching any existing code**:

1. **Metadata-only** (preferred). Insert one row into `TaskTypeMetadata` and one
   row per validation rule into `TaskFieldDefinition`. The `Marketing` type in
   the seed data is a complete worked example.
2. **Code-backed handler** (only when rules are not declaratively expressible).
   Implement `IRegisterableTaskHandler` anywhere in the assembly. The DI
   registration in `TaskHandlerRegistrationExtensions.AddTaskHandlersFromAssembly`
   picks it up by reflection.

Full walkthrough: `docs/EXTENSION_GUIDE.md`.

## Configuration

| Setting | Default | Effect |
|--------|---------|--------|
| `ConnectionStrings:DefaultConnection` | _(none)_ | SQL Server connection string. |
| `TaskTypeConflictValidation:FailOnConflict` | `false` | When `true`, the app refuses to start if more than one rule provider claims the same task-type code. |

## Documentation

- `docs/WORKFLOW.md` — workflow rules and per-type providers
- `docs/EXTENSION_GUIDE.md` — adding new task types and validation rules
- `docs/API_ERROR_CODES.md` — error-code catalog returned by the API
- `docs/QUICKSTART.md` — runtime walkthrough with sample requests
- `docs/BEST_PRACTICES.md` — coding conventions for this codebase

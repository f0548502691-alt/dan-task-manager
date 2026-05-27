# Dan Task Manager backend

.NET 8 API for generic task workflows backed by EF Core and SQL Server. The
backend exposes task, user, and task-type metadata endpoints used by the Angular
workflow board.

## Local setup

From the repository root:

```bash
docker compose up --build
```

The backend container listens on `http://localhost:8080` and connects to the SQL
Server container using the `ConnectionStrings__DefaultConnection` value in
`docker-compose.yml`.

For direct local runs from `backend/`:

```bash
dotnet restore
dotnet run
```

`Program.cs` applies EF migrations when present, falls back to `EnsureCreated`,
and then runs `HybridSchemaBootstrapper.EnsureSchema` to create metadata tables,
JSON check constraints, computed JSON indexes, and seed metadata for the built-in
task types.

## Main components

| Area | Files | Responsibility |
|------|-------|----------------|
| HTTP API | `Controllers/*.cs` | Validate requests, send MediatR commands/queries, shape responses |
| Request contracts | `Contracts/Requests/**` | Public request DTOs such as `customFields` and pagination |
| Application services | `Services/TaskApplicationService.cs`, `Services/UserApplicationService.cs` | Query and mutate tasks/users through EF Core |
| Workflow engine | `Services/TaskWorkflowService.cs` | Status movement, close rules, assignee checks, immutability |
| Rule providers | `Services/TaskWorkflowRuleProviders.cs` | Metadata-first task validation with handler fallback |
| Task metadata | `Services/TaskTypeValidationService.cs`, `Data/HybridSchemaBootstrapper.cs` | Runtime task type schemas and custom field rules |
| Handlers | `Domain/Handlers/*.cs` | Built-in fallback validation for Procurement and Development |
| DTO projections | `Services/TaskDtoMappings.cs`, `Services/QueryModels.cs` | Paged summary/detail response models |

## Workflow model

- Supported task types are `Procurement` and `Development`
  (`WorkflowConstants.SupportedTaskTypes`).
- Created status is `1`; closed status is `99`.
- Forward status movement must be exactly `+1`.
- Backward movement can target any lower status down to `1`.
- Closing is only allowed through `POST /api/tasks/{id}/close` and only from the
  final status for the task type.
- Closed tasks cannot be changed, updated, or deleted.

Built-in final statuses and required fields:

| Task type | Final status | Required custom fields |
|-----------|--------------|------------------------|
| Procurement | 3 | status 2: `prices` array with two strings; status 3: `receipt` string |
| Development | 4 | status 2: `specification`; status 3: `branchName`; status 4: `versionNumber` |

Database metadata can define the same rules and takes precedence over handlers
because `MetadataTaskWorkflowRuleProvider` has priority `0`; handler fallback has
priority `100`.

## Public API conventions

- Public requests and responses use `customFields`.
- `BaseTask.CustomDataJson` is the internal SQL Server JSON storage field.
- List endpoints return `PagedResult<TaskSummaryDto>` and omit `customFields`.
- Detail/create/status/close responses return task details with `customFields`.
- `page` defaults to `1`; `pageSize` defaults to `20` and is capped at `100`.
- API errors normally use `{ "error": "...", "code": "..." }`.

See [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md) for endpoint examples
and [API_ERROR_CODES.md](API_ERROR_CODES.md) for troubleshooting.

## Common commands

```bash
# Restore packages
dotnet restore

# Run tests
dotnet test

# Run API locally
dotnet run
```

If `dotnet` is unavailable in an automation environment, validate documentation
changes with `git diff --check` and run frontend checks separately from
`frontend/`.

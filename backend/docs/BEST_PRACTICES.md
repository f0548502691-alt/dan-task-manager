# Best Practices

These conventions keep the backend consistent with the current architecture.
They are based on the code paths in `Program.cs`, `Controllers/`,
`Application/Tasks/`, `Services/`, `Domain/`, and `Data/ApplicationDbContext.cs`.

## Layering

| Layer | Responsibility | Avoid |
|-------|----------------|-------|
| Controllers | HTTP routing, request validation, translating results to API responses | Business rules, EF queries, task-type validation |
| MediatR commands/queries | Use-case entry points under `Application/Tasks/<UseCase>` | Reimplementing workflow logic |
| Application services | Task/user orchestration and persistence | HTTP-specific response construction |
| `TaskWorkflowService` | General workflow invariants: status movement, close rules, assignee checks, JSON object checks | Per-type field rules |
| Rule providers | Per-type validation from metadata or handlers | Database writes |
| EF model | Schema, constraints, seed data, relationships | Runtime-only behavior |

Controllers should throw `ApiValidationException`, `ApiNotFoundException`, or
`WorkflowValidationException` for error responses. The global middleware emits
the public `{ error, code }` shape.

## Public API DTOs

Use the current request property names:

```json
{
  "taskType": "Marketing",
  "description": "Launch campaign",
  "assignedToUserId": 1,
  "customFields": {}
}
```

```json
{
  "newStatus": 2,
  "nextAssignedToUserId": 2,
  "customFields": {
    "campaignName": "Spring campaign",
    "targetAudience": "B2B"
  }
}
```

Do not add new client-facing uses of `customDataJson` or `newDataJson`.
`BaseTask.CustomDataJson` is the storage column; controllers map public
`customFields` into that internal JSON string.

List endpoints return `PagedResult<T>` with `items`, `page`, `pageSize`,
`totalCount`, and `totalPages`. List task summaries intentionally omit
`customFields`; use task detail reads for editable custom data.

## Workflow rules

Status constants live in `Domain/WorkflowConstants.cs`:

- Created status: `1`
- Closed status: `99`

General rules:

- Forward movement must be exactly `+1`.
- Backward movement may target any lower status greater than or equal to `1`.
- The same status is invalid.
- Status `99` is reachable only through `POST /api/tasks/{id}/close`.
- Close requires the task type's final status.
- Closed tasks cannot be updated, deleted, moved, or closed again.
- Status changes and close requests require an existing `nextAssignedToUserId`.
- `customFields` must be a JSON object.

When adding behavior, keep these invariants in `TaskWorkflowService`; add
task-type-specific rules through metadata or an `IRegisterableTaskHandler`.

## Task-type extension rules

Prefer metadata:

- Add or update `TaskTypeMetadata`.
- Add one `TaskFieldDefinition` row per field rule.
- Use `GET /api/task-types` to verify what the frontend sees.

Use handlers only for custom logic:

- Implement `IRegisterableTaskHandler`.
- Do not manually register it; `AddTaskHandlersFromAssembly` discovers it.
- Keep handlers focused on validation and close-data behavior delegated through
  rule providers.

Avoid duplicate rule sources for a task type. Metadata provider priority `0`
wins over handler provider priority `100`, so a duplicate handler may never run.
Enable `TaskTypeConflictValidation:FailOnConflict` when you want startup to fail
instead of logging a warning.

## EF Core and migrations

The EF model lives in `Data/ApplicationDbContext.cs`; the current repository
includes an initial migration under `Migrations/`.

When changing schema or seed data:

1. Update entities and model configuration.
2. Generate a descriptive migration from `/backend`:

   ```bash
   dotnet ef migrations add AddSomeFeature
   ```

3. Review the generated migration for constraints, delete behavior, indexes, and
   deterministic seed values.
4. Commit the migration and `Migrations/ApplicationDbContextModelSnapshot.cs`.

Do not create another initial migration for setup. Startup currently applies
migrations automatically because migrations exist; deployments may still choose
to run `dotnet ef database update` out-of-band.

For indexed custom fields, set `IsIndexed = true` only for scalar values.
`JsonIndexBootstrapper` creates SQL Server computed columns and indexes for
`string`, `number`, and `stringOrNumber` fields and skips arrays/objects.

## Error handling

Use stable error codes:

```csharp
throw new ApiValidationException("Description is required");
throw new ApiNotFoundException("Task not found");
throw new WorkflowValidationException(result.Message, result.Code);
```

Add new codes deliberately and document them in `docs/API_ERROR_CODES.md`.
Clients should not parse localized or human-readable text.

## Testing

Match test coverage to the layer being changed:

- Field-rule or handler change: unit tests for valid and invalid payloads.
- Workflow invariant change: `TaskWorkflowService` tests for status movement,
  close behavior, assignee checks, and closed-task immutability.
- Application/MediatR change: command/query handler tests.
- EF projection or query shape change: tests for `PagedResult<T>`, summaries,
  and details.
- API contract change: request validation and response/error shape tests.
- Migration or schema change: verify the generated migration and snapshot.

Run the backend suite from `/backend`:

```bash
dotnet test
```

The Angular scaffold currently has a placeholder test script:

```bash
npm --prefix frontend test
```

Run the frontend build when changing TypeScript, templates, CSS, dependencies,
or public API assumptions:

```bash
npm --prefix frontend run build
```

## Documentation updates

Update docs in the same change when behavior affects:

- Setup or migration steps (`README.md`, `docs/QUICKSTART.md`).
- Public API request/response/error contracts (`docs/API_ERROR_CODES.md`).
- Workflow rules or provider behavior (`docs/WORKFLOW.md`).
- Extension mechanisms (`docs/EXTENSION_GUIDE.md`).
- Frontend expectations, if a frontend guide exists on the branch.

Prefer updating existing docs over creating overlapping pages.

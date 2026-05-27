# Documentation Index

This index points to the documentation that matches the current task workflow
implementation.

## Start here

| Document | Use it for |
|----------|------------|
| [README.md](README.md) | Backend architecture, setup, task type model, and API overview. |
| [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md) | Workflow state machine, provider chain, and request/response examples. |
| [EXTENSION_GUIDE.md](EXTENSION_GUIDE.md) | Adding metadata-backed task types, code handlers, and frontend schema support. |
| [STRATEGY_PATTERN_DOCS.md](STRATEGY_PATTERN_DOCS.md) | Task type catalog, handler registration, and strategy-provider details. |
| [API_ERROR_CODES.md](API_ERROR_CODES.md) | Error responses and troubleshooting checklist. |

## Current implementation map

| Area | Primary files |
|------|---------------|
| REST API | `Controllers/TasksController.cs`, `Controllers/TaskTypesController.cs` |
| MediatR task operations | `Application/Tasks/**` |
| Application service | `Services/TaskApplicationService.cs` |
| Workflow state machine | `Services/TaskWorkflowService.cs` |
| Rule providers | `Services/TaskWorkflowRuleProviders.cs` |
| Task type catalog | `Services/TaskTypeCatalogService.cs` |
| Metadata validation/schema | `Services/TaskTypeValidationService.cs` |
| Handler discovery | `Services/TaskHandlerRegistrationExtensions.cs` |
| Code-backed handlers | `Domain/Handlers/*TaskHandler.cs` |
| EF model and seed data | `Data/ApplicationDbContext.cs`, `Data/HybridSchemaBootstrapper.cs` |
| Summary projection | `Services/TaskProjectionExpressions.cs` |

## Key facts

- New tasks start at status `1`.
- Closed tasks use status `99`.
- Public API payloads use `customFields`, not `customDataJson` or `newDataJson`.
- List endpoints return `PagedResult<TaskSummaryDto>` and omit `customFields`.
- Detail/create/status-change/close responses return task details with parsed
  `customFields`.
- Supported task types come from `TaskTypeCatalogService`, which merges active
  metadata and registerable handlers.
- Metadata rule provider priority `0` runs before handler provider priority `100`.
- Only handlers implementing `IRegisterableTaskHandler` are auto-registered as
  public code-backed task types.

## Developer workflows

### Add a declarative task type

1. Add metadata through `POST /api/task-types`.
2. Add field rules through `POST /api/task-types/{taskType}/fields` or
   `PUT /api/task-types/{taskType}/fields/{field}`.
3. Verify the schema with `GET /api/task-types/{taskType}`.
4. Exercise create/status/close with public `customFields` payloads.

See [EXTENSION_GUIDE.md](EXTENSION_GUIDE.md).

### Add custom handler validation

1. Implement `IRegisterableTaskHandler`.
2. Keep `TaskType` unique, case-insensitively.
3. Add direct handler tests and workflow/catalog tests.
4. Verify `GET /api/task-types` returns a handler-backed schema with `fields: []`.

See [STRATEGY_PATTERN_DOCS.md](STRATEGY_PATTERN_DOCS.md).

### Debug workflow failures

1. Read the error body.
2. Check status movement and final status in
   [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md).
3. Check field requirements from `GET /api/task-types`.
4. Use [API_ERROR_CODES.md](API_ERROR_CODES.md) for common fixes.

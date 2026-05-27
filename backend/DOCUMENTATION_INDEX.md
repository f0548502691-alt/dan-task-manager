# DanTaskManager documentation index

Use this index to find the maintained backend docs quickly. Several older
markdown files remain as historical implementation notes; prefer the documents
listed below for current API and workflow behavior.

## Current references

| Document | Use it for |
| --- | --- |
| [README.md](README.md) | Backend architecture, setup, task operation summary, and common pitfalls. |
| [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md) | Current task workflow API, examples, status rules, close rules, and runbook. |
| [API_ERROR_CODES.md](API_ERROR_CODES.md) | Error response shape, stable error codes, validation failures, and troubleshooting. |
| [STRATEGY_PATTERN_DOCS.md](STRATEGY_PATTERN_DOCS.md) | Handler/strategy pattern background for code-backed task types. |
| [EXTENSION_GUIDE.md](EXTENSION_GUIDE.md) | Guidance for adding or extending task types and metadata. |

## Codepaths covered by the current docs

| Area | Codepaths |
| --- | --- |
| Task HTTP operations | `Controllers/TasksController.cs`, `Contracts/Requests/Tasks/*` |
| User task reads | `Controllers/UsersController.cs`, `Services/UserApplicationService.cs` |
| MediatR commands and queries | `Application/Tasks/*` |
| Task facade | `Services/TaskApplicationService.cs`, `Services/ITaskApplicationService.cs` |
| Workflow state machine | `Services/TaskWorkflowService.cs`, `Domain/WorkflowConstants.cs` |
| Rule provider chain | `Services/TaskWorkflowRuleProviders.cs`, `Services/TaskTypeValidationService.cs` |
| Task type catalog | `Services/TaskTypeCatalogService.cs`, `Controllers/TaskTypesController.cs` |
| DTOs and pagination | `Services/QueryModels.cs`, `Services/TaskProjectionExpressions.cs` |
| Tests | `Tests/*CommandHandlerTests.cs`, `Tests/TaskMediatorAdditionalHandlerTests.cs`, `Tests/WorkflowServiceTests.cs`, `Tests/TaskApplicationServiceTests.cs` |

## Current task workflow facts

- Created status is `1`; closed status is `99`.
- The public API uses `customFields`. The database column is `CustomDataJson`.
- `POST /api/tasks` creates a task at status `1`.
- `GET /api/tasks/{id}` returns detail with `customFields`.
- List endpoints return `PagedResult<TaskSummaryDto>` and omit
  `customFields`.
- `POST /api/tasks/{id}/change-status` requires `newStatus`,
  `nextAssignedToUserId`, and object `customFields`.
- Forward movement must be exactly one status; rollback can move to any lower
  status that is still at least `1`.
- `POST /api/tasks/{id}/close` requires `nextAssignedToUserId` and
  `finalNotes`; it only works from the task type final status.
- Closed tasks cannot change status, update description, or be deleted.
- Assigned-task list endpoints include closed tasks; use `currentStatus != 99`
  when a consumer needs only open work.

## Setup and verification commands

```bash
dotnet restore backend/DanTaskManager.csproj
dotnet run --project backend/DanTaskManager.csproj
dotnet test backend/DanTaskManager.csproj
```

The backend project references xUnit, Moq, and the .NET test SDK directly in
`DanTaskManager.csproj`, so the test command runs the in-repo unit tests without
a separate test project.

## Historical docs

The repository includes generated/historical files such as
`IMPLEMENTATION_COMPLETE.md`, `WORKFLOW_IMPLEMENTATION.md`, `FINAL_REPORT.md`,
and quick-start variants. They may describe earlier contracts such as status `0`
starts, `newDataJson`, `customDataJson` HTTP bodies, or non-paged arrays. Verify
behavior against source before using those notes for implementation decisions.

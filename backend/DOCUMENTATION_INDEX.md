# Backend documentation index

Use this index to find the current backend workflow references. Prefer these
files over older generated summaries when contracts disagree.

## Current references

| Document | Use it for |
| --- | --- |
| [README.md](README.md) | Backend setup, source map, seed data, API contract highlights |
| [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md) | Workflow state machine, supported task types, endpoint examples |
| [API_ERROR_CODES.md](API_ERROR_CODES.md) | HTTP statuses, validation errors, response shapes |
| [STRATEGY_PATTERN_DOCS.md](STRATEGY_PATTERN_DOCS.md) | Rule provider architecture and handler fallback behavior |
| [EXTENSION_GUIDE.md](EXTENSION_GUIDE.md) | Checklist for changing task types or adding backend features |

## Current workflow facts

- Supported task types are `Procurement` and `Development` only.
- New tasks start at status `1`; status `99` is closed.
- Forward status movement must be exactly `+1`; rollback can move to lower
  statuses down to `1`.
- Closing is allowed only from the task type final status.
- Status changes require `newStatus`, `nextAssignedToUserId`, and a
  `customFields` JSON object.
- List endpoints return `PagedResult<TaskSummaryDto>` without `customFields`.
  Use `GET /api/tasks/{id}` for `TaskDetailsDto`.
- Metadata validation has priority over handler validation for active supported
  task types.

## Source map

| Area | Files |
| --- | --- |
| API controllers | `Controllers/TasksController.cs`, `Controllers/TaskTypesController.cs` |
| Workflow rules | `Services/TaskWorkflowService.cs`, `Services/TaskWorkflowRuleProviders.cs` |
| Application/query layer | `Services/TaskApplicationService.cs`, `Services/QueryModels.cs` |
| Metadata validation | `Services/TaskTypeValidationService.cs`, `Data/ApplicationDbContext.cs` |
| Supported-type constants | `Domain/WorkflowConstants.cs` |
| Request validation | `Validation/TaskRequestValidators.cs` |
| Global error shape | `Middleware/GlobalExceptionMiddleware.cs` |

## Known stale patterns to avoid

- Do not use examples that start tasks at status `0`.
- Do not send `newDataJson` or `customDataJson` in public API requests; send
  `customFields`.
- Do not assume metadata writes can introduce arbitrary new task types. They are
  restricted by `WorkflowConstants.SupportedTaskTypes`.
- Do not assume list responses include custom task data. They are summaries.

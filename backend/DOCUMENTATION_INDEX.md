# Documentation Index

Use this index to find the current backend documentation. Several older
`IMPLEMENTATION_*`, `FINAL_*`, and quick-start documents were written as project
completion notes; they can be useful for historical context but may mention
removed internals such as `TaskStatusService` or status `0`. The documents below
are the authoritative references for current development.

## Start here

| Document | Use it for |
|----------|------------|
| [README.md](README.md) | Backend architecture, setup, API contract overview, and common pitfalls |
| [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md) | Current workflow service behavior, status rules, payload examples, and paged responses |
| [API_ERROR_CODES.md](API_ERROR_CODES.md) | Current HTTP status codes, FluentValidation messages, and middleware error shapes |
| [EXTENSION_GUIDE.md](EXTENSION_GUIDE.md) | Adding task handlers, validators, workflow rules, and endpoints |

## Current code map

```
backend/
├── Controllers/
│   ├── TasksController.cs
│   ├── UsersController.cs
│   └── PaginationQuery.cs
├── Data/
│   └── ApplicationDbContext.cs
├── Domain/
│   ├── AppUser.cs
│   ├── BaseTask.cs
│   ├── WorkflowConstants.cs
│   ├── WorkflowValidationException.cs
│   └── Handlers/
│       ├── ITaskHandler.cs
│       ├── StatusValidationTaskHandlerBase.cs
│       ├── ProcurementTaskHandler.cs
│       ├── DevelopmentTaskHandler.cs
│       └── TaskHandlerFactory.cs
├── Middleware/
│   └── GlobalExceptionMiddleware.cs
├── Services/
│   ├── ITaskApplicationService.cs
│   ├── TaskApplicationService.cs
│   ├── ITaskWorkflowService.cs
│   ├── TaskWorkflowService.cs
│   ├── IUserApplicationService.cs
│   ├── UserApplicationService.cs
│   ├── QueryModels.cs
│   └── TaskHandlerRegistrationExtensions.cs
├── Validation/
│   ├── TaskRequestValidators.cs
│   └── UserRequestValidators.cs
└── Tests/
    ├── HandlerTests.cs
    └── WorkflowServiceTests.cs
```

## Common questions

### How do I call the API?

Read [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md). It includes request
examples for create, status change with reassignment, rollback, close, and paged
query responses.

### Why did a request return 400?

Read [API_ERROR_CODES.md](API_ERROR_CODES.md). First check whether the response
has a `code` field:

- no `code`: controller/application validation failed;
- `workflow_validation_failed`: workflow or handler validation failed;
- `internal_server_error`: unexpected exception.

### How do I add a new task type?

Read [EXTENSION_GUIDE.md](EXTENSION_GUIDE.md). In short:

1. Add an `ITaskHandler` implementation under `DanTaskManager.Domain.Handlers`.
2. Use `StatusValidationTaskHandlerBase` when validation depends only on target
   status and `newDataJson`.
3. Do not manually register the handler; the assembly scanner registers concrete
   handlers from the handler namespace.
4. Add handler and workflow tests.

### Where do validation rules belong?

- Request shape and syntax: `Validation/*RequestValidators.cs`.
- Database-backed checks: application or workflow services.
- Cross-task workflow invariants: `TaskWorkflowService`.
- Task-type payload rules: the relevant `ITaskHandler`.

### Which status numbers are current?

- `WorkflowConstants.CreatedStatus` is `1`.
- `WorkflowConstants.ClosedStatus` is `99`.
- Forward movement is exactly `+1`.
- Rollback can move to any lower status starting at `1`.
- Handler `FinalStatus` is the last normal status before close.

## Historical docs

The following documents may still help explain earlier design intent, but verify
their claims against source code or the current references above before using
them for implementation:

- `BEST_PRACTICES.md`
- `GETTING_STARTED.md`
- `MASTER_REFERENCE.md`
- `QUICK_GUIDE.md`
- `QUICKSTART*.md`
- `IMPLEMENTATION_*.md`
- `FINAL_*.md`
- `PROJECT_COMPLETION.md`
- `STRATEGY_PATTERN_DOCS.md`
- `WORKFLOW_IMPLEMENTATION.md`

When behavior changes, update the authoritative references first.

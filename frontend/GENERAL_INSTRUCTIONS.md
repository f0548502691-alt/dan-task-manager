# Frontend General Instructions

This file is the default decision guide for client-side architecture choices.

## State Management Policy

### Current default
- Use Angular Signals in services/components for small-to-medium scoped state.
- Do **not** add Signal Store by default.

### When Signal Store is needed
Adopt Signal Store when at least one of these is true:

1. State is shared across multiple unrelated feature areas.
2. Update logic becomes duplicated across several services/components.
3. You need clear, centralized updaters/effects for async flows.
4. Selectors/computed derivations are becoming complex and repetitive.
5. Testing state transitions is difficult with the current structure.

### Decision checklist (quick)
- If 0-1 items above are true -> stay with service Signals.
- If 2+ items are true -> move to Signal Store.

## Current project decision
- For the current task workflow UI, existing Angular Signals in `TaskService` are sufficient.
- No immediate migration to Signal Store is required.

## Client Baseline (Angular)

Use the following checklist as the default baseline for client-side task workflow work:

- Framework: Angular.
- Leverage Angular built-ins: dependency injection, services, and reactive patterns.
- TypeScript must stay in strict mode (`"strict": true` in `tsconfig` when present).
- Component architecture should stay focused; keep templates clean and split domain-specific fields into dedicated components.
- UI should remain minimal and functionality-first (avoid unnecessary styling complexity).
- Required capabilities:
  - Create task.
  - Manage lifecycle (advance, reverse, close).
  - View current user's tasks.
- A hard-coded user ID is acceptable for MVP flows.

### Verification snapshot (current repository state)

- [OK] Angular + DI/service/reactive patterns are implemented in `src/app/tasks/task.service.ts` and related components.
- [OK] Focused component architecture is in place (`task-workflow-board`, `procurement-fields`, `development-fields`).
- [OK] Minimal UI approach is in place (simple layout and lightweight styles).
- [OK] Viewing user tasks is implemented (`TaskService.refreshCurrentUserTasks()` + board list rendering).
- [OK] Advancing/reversing task status is available through status selection in the workflow board.
- [OK] Creating a task from the UI is implemented in `task-workflow-board` via `submitCreateTask()`.
- [OK] Closing a task from the UI is implemented in `task-workflow-board` via `submitCloseTask()`.
- [OK] Hard-coded user ID wiring is implemented in `TaskWorkflowBoardComponent` (`DEFAULT_CURRENT_USER_ID = 1` + `setCurrentUserId` on init).
- [OK] Strict mode is explicitly configured in `frontend/tsconfig.json` (`"strict": true`).

## Task Workflow UI Reference

Use this section when changing the Angular task workflow surface or aligning it with the backend API.

### Source map

- `src/app/tasks/task.interfaces.ts`: shared client DTOs, status constants, and final-status mappings.
- `src/app/tasks/task.service.ts`: API access and local signal state for the selected current user's open tasks.
- `src/app/tasks/task-workflow-board.component.ts`: create, select, status-change, and close-task form orchestration.
- `src/app/tasks/procurement-fields.component.*`: status-specific Procurement validators and controls.
- `src/app/tasks/development-fields.component.*`: status-specific Development validators and controls.

### State ownership

- Keep cross-component task state in `TaskService` signals: `currentUserId`, `tasks`, `isLoading`, and `error`.
- Keep transient form state in `TaskWorkflowBoardComponent`; child field components only attach validators to controls supplied by the parent form.
- `TaskService.syncTaskWithState()` keeps the current user's list newest-first by `createdAt`. It removes tasks assigned to another user or tasks moved to closed status `99`.
- The MVP board hard-codes `DEFAULT_CURRENT_USER_ID = 1`. Replace this in one place when authentication/current-user context is introduced.

### API calls used by the board

```text
GET  /api/tasks/user/{userId}
POST /api/tasks
POST /api/tasks/{taskId}/change-status
POST /api/tasks/{taskId}/close
PUT  /api/tasks/{taskId}
DELETE /api/tasks/{taskId}
```

- Create task payload:

```json
{
  "taskType": "Development",
  "description": "Implement invoice export",
  "assignedToUserId": 1
}
```

- Status-change payload:

```json
{
  "newStatus": 2,
  "newDataJson": "{\"specification\":\"Export invoices as CSV\"}"
}
```

- Close payload:

```json
{
  "finalNotes": "Released and validated by QA"
}
```

### Workflow constraints

- Shared statuses are `0` backlog, `1` in progress, `2` ready for review, `3` done, `4` released, and `99` closed.
- Backend workflow movement allows exactly one step forward, any lower status backward, and rejects same-status updates.
- Closed tasks cannot be changed. Closing a task writes `finalNotes` and `closedAt` into `customDataJson` and moves the task to status `99`.
- Procurement final status is `3`; Development final status is `4`.
- Unknown task types can be created by the backend, but the current create form only offers Procurement and Development.

### Type-specific payloads

| Task type | Target status | Required JSON key | Client control | Backend constraint |
| --- | --- | --- | --- | --- |
| Procurement | `2` | `prices` | `priceA`, `priceB` | Array of exactly 2 non-empty strings |
| Procurement | `3` | `receipt` | `receipt` | Non-empty string |
| Development | `2` | `specification` | `specification` | String with at least 10 characters |
| Development | `3` | `branchName` | `branchName` | Non-empty string; no spaces, `//`, trailing `/`, or trailing `.` |
| Development | `4` | `versionNumber` | `versionNumber` | Non-empty string or number; dotted versions must have numeric parts |

Examples:

```json
{ "prices": ["1200", "1350"] }
{ "receipt": "PO-2026-0042" }
{ "specification": "Add CSV invoice export with filters" }
{ "branchName": "feature/invoice-export" }
{ "versionNumber": "1.2.0" }
```

### Integration pitfalls

- `TaskService.refreshCurrentUserTasks()` currently expects `GET /api/tasks/user/{id}` to return `BaseTaskDto[]`.
- The backend controller currently returns `PagedResult<TaskSummaryDto>` for that route. That shape is `{ items, page, pageSize, totalCount, totalPages }`, and summaries do not include `customDataJson`.
- Until the client or backend contract is aligned, the task list fetch and selected-task hydration are not a reliable source of `customDataJson` for existing tasks.
- Keep `frontend/tsconfig.json` strict. The current repo does not include a frontend package manifest or build script, so add standard Angular tooling before documenting npm-based verification commands.

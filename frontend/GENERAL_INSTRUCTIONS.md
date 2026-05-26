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

Use this section when changing the Angular task workflow board or aligning it with backend workflow behavior.

### Codepaths
- `src/app/tasks/task-workflow-board.component.ts` owns the create, status update, close, and selected-task form flows.
- `src/app/tasks/task.service.ts` owns API calls, service-level Signals, newest-first task sorting, and state synchronization after mutations.
- `src/app/tasks/task.interfaces.ts` mirrors task DTOs, workflow request/response shapes, status constants, labels, and type-specific final statuses.
- `src/app/tasks/procurement-fields.component.*` and `src/app/tasks/development-fields.component.*` own status-specific form fields and validators.
- `frontend/tsconfig.json` and `frontend/tsconfig.app.json` enable strict TypeScript checks for frontend source.

### Runtime assumptions
- The MVP board hard-codes the current user as `1` (`DEFAULT_CURRENT_USER_ID`) and calls `TaskService.setCurrentUserId(1)` on init.
- `TaskService.refreshCurrentUserTasks()` calls `GET /api/tasks/user/{userId}` and stores the result in a Signal consumed by the board.
- Tasks are displayed newest first by `createdAt`.
- Closed tasks use status `99`; `TaskService.syncTaskWithState()` removes closed tasks from the current open-task list.
- Errors from HTTP calls are normalized into `TaskService.error`; components clear that signal before new create/update/close attempts.

### Create task flow
The create form collects:

```json
{
  "taskType": "Procurement",
  "description": "Compare supplier quotes",
  "assignedToUserId": 1
}
```

Client constraints:
- `taskType` is selected from `Procurement` or `Development`.
- `description` is required and must contain at least 5 characters.
- `customDataJson` is omitted by the UI; the backend defaults missing custom data to `{}`.
- On success, the board selects the returned task and clears only the description field.

### Status update flow
The board posts status changes through `TaskService.changeTaskStatus()`:

```json
{
  "newStatus": 2,
  "newDataJson": "{\"prices\":[\"100\",\"125\"]}"
}
```

Backend workflow constraints verified in `backend/Services/TaskWorkflowService.cs`:
- Forward movement must be exactly one status above the current status.
- Reverse movement is allowed to any lower status.
- Re-submitting the current status is rejected.
- Closed tasks (`99`) cannot be changed.
- Handler final statuses cap forward movement for known task types (`Procurement` -> `3`, `Development` -> `4`).

The client suggests the next forward status but still renders lower statuses for reverse moves. If a user selects the current status, the backend returns a workflow validation error.

### Type-specific payloads
The form components build payloads that match the backend handlers:

| Task type | Status | Payload | Important constraints |
|-----------|--------|---------|-----------------------|
| `Procurement` | `2` (`Ready for Review`) | `{"prices":["100","125"]}` | Exactly two non-empty string prices. |
| `Procurement` | `3` (`Done`) | `{"receipt":"PO-123"}` | Non-empty string receipt. |
| `Development` | `2` (`Ready for Review`) | `{"specification":"Build the import flow"}` | String with at least 10 characters. |
| `Development` | `3` (`Done`) | `{"branchName":"feature/import-flow"}` | Non-empty string. Client rejects spaces; backend also rejects `//`, trailing `/`, and trailing `.`. |
| `Development` | `4` (`Released`) | `{"versionNumber":"1.0.0"}` | Non-empty string/number; dotted versions must have numeric parts. |

Unknown task types fall back to the raw JSON textarea. The fallback payload must parse to a JSON object; arrays and primitives are treated as `{}` by the client.

### Close task flow
The close form posts:

```json
{
  "finalNotes": "Released to production"
}
```

Client constraints:
- `finalNotes` is required and must contain at least 3 characters.
- The close button is disabled for already closed tasks.

Backend behavior verified in `backend/Services/TaskWorkflowService.cs`:
- Closing sets `CurrentStatus` to `99`.
- `finalNotes` and an ISO `closedAt` timestamp are merged into `CustomDataJson`.
- Closed tasks are excluded from the open user-task query and removed from the client state after the response.

### Integration pitfalls
- Keep `TASK_STATUS`, `DEFAULT_STATUS_LABELS`, and `TASK_FINAL_STATUS_BY_TYPE` aligned with backend handler final statuses.
- When adding a new task type, add a backend handler first if it needs workflow validation, then add a dedicated Angular field component only if the generic JSON fallback is not enough.
- The current `TaskService.refreshCurrentUserTasks()` type expects an array of tasks, while the current backend controller returns a `PagedResult<TaskSummaryDto>` for `GET /api/tasks/user/{userId}`. Reconcile this contract before relying on the list flow beyond local MVP usage.
- Strict TypeScript options include `noPropertyAccessFromIndexSignature`; access reactive form controls by bracket notation (`controls['fieldName']`) unless the form type guarantees named properties.

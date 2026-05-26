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

Use this section when changing the Angular task board or the backend workflow endpoints it calls.

### Source files
- `src/app/tasks/task.interfaces.ts`: shared client DTOs, status constants, and final status by task type.
- `src/app/tasks/task.service.ts`: HTTP calls plus signal-owned list/loading/error state.
- `src/app/tasks/task-workflow-board.component.ts`: create, select, status update, close, and payload-building forms.
- `src/app/tasks/procurement-fields.component.*`: Procurement-specific status fields and validators.
- `src/app/tasks/development-fields.component.*`: Development-specific status fields and validators.
- Backend contracts are currently implemented by `backend/Controllers/TasksController.cs`,
  `backend/Services/TaskWorkflowService.cs`, `backend/Domain/Handlers/ProcurementTaskHandler.cs`,
  and `backend/Domain/Handlers/DevelopmentTaskHandler.cs`.

### Runtime model
- The board initializes with `DEFAULT_CURRENT_USER_ID = 1` and calls `TaskService.setCurrentUserId(1)`.
- `TaskService` owns the current user ID, task list, loading flag, and last error as Angular Signals.
- The board owns form state and only writes task state through `TaskService`.
- The create form only offers `Procurement` and `Development`.
- Tasks are displayed newest-first by `createdAt`. Closed tasks (`99`) are removed from the local list.
- `TASK_STATUS.CLOSED` is `99`; do not reuse that value for an intermediate workflow state.
- Type-specific final statuses are:
  - `Procurement`: `DONE` (`3`)
  - `Development`: `RELEASED` (`4`)

### API calls used by the board

Create task:

```http
POST /api/tasks
Content-Type: application/json

{
  "taskType": "Development",
  "description": "Implement import workflow",
  "assignedToUserId": 1
}
```

The backend creates tasks at status `0` and normalizes missing `customDataJson` to `{}`.

Load current user's open tasks:

```http
GET /api/tasks/user/1
```

Important integration constraint: `TaskService.refreshCurrentUserTasks()` currently types this response as
`BaseTaskDto[]`, but the backend action returns `PagedResult<TaskSummaryDto>` with an `items` array and no
`customDataJson` on each summary item. Align this contract before relying on the board against the live API.

Change status:

```http
POST /api/tasks/{taskId}/change-status
Content-Type: application/json

{
  "newStatus": 2,
  "newDataJson": "{\"specification\":\"At least ten characters\"}"
}
```

Close task:

```http
POST /api/tasks/{taskId}/close
Content-Type: application/json

{
  "finalNotes": "Released and handed off"
}
```

Closing sets status `99` and appends `finalNotes` plus `closedAt` into the task JSON on the backend.

### Status movement rules
- Forward movement must be exactly one status at a time.
- Backward movement to any lower non-negative status is allowed.
- Re-submitting the current status is rejected by the backend.
- A closed task cannot be updated or closed again.
- Known task handlers reject movement beyond their final status.
- Unknown task types can be created through the backend API, but not through the current create form. If one is
  loaded into the board, the UI only exposes fallback JSON for statuses already selectable by the board. Add
  explicit UI support before expecting custom task types to advance smoothly.

### Workflow payload matrix

Procurement payloads:

| New status | UI fields | JSON sent in `newDataJson` | Backend constraints |
| --- | --- | --- | --- |
| `2` (`READY_FOR_REVIEW`) | `priceA`, `priceB` | `{ "prices": ["100", "120"] }` | `prices` must be exactly two non-empty strings. |
| `3` (`DONE`) | `receipt` | `{ "receipt": "PO-12345" }` | `receipt` must be a non-empty string. |

Development payloads:

| New status | UI fields | JSON sent in `newDataJson` | Backend constraints |
| --- | --- | --- | --- |
| `2` (`READY_FOR_REVIEW`) | `specification` | `{ "specification": "At least ten characters" }` | At least 10 characters. |
| `3` (`DONE`) | `branchName` | `{ "branchName": "feature/import-workflow" }` | Non-empty string, no spaces, no `//`, and cannot end with `/` or `.`. |
| `4` (`RELEASED`) | `versionNumber` | `{ "versionNumber": "1.0.0" }` | Non-empty string or number; dotted parts must be numeric. |

### Extension checklist
- Add status labels and final status constants in `task.interfaces.ts`.
- Add or update a backend `ITaskHandler` for task-type validation and final status behavior.
- Add a focused Angular field component for non-trivial type-specific forms.
- Keep payload keys identical between the Angular builder and backend handler validation.
- Update `statusOptions()` and `getSuggestedStatus()` if a type needs a non-linear or longer workflow.
- Keep strict TypeScript settings passing (`strict`, `noPropertyAccessFromIndexSignature`,
  `noImplicitReturns`, and `noFallthroughCasesInSwitch`).

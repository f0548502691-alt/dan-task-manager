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
- A default current user ID is acceptable for loading the board, but create and
  status-change flows must expose assignment fields because the backend requires
  `assignedToUserId` and `nextAssignedToUserId`.

### Verification snapshot (current repository state)

- [OK] Angular + DI/service/reactive patterns are implemented in `src/app/tasks/task.service.ts` and related components.
- [OK] Focused component architecture is in place (`task-workflow-board`, `procurement-fields`, `development-fields`).
- [OK] Minimal UI approach is in place (simple layout and lightweight styles).
- [OK] Viewing user tasks is implemented (`TaskService.refreshCurrentUserTasks()` + board list rendering).
- [OK] Advancing/reversing task status is available through status selection in the workflow board.
- [OK] Creating a task from the UI is implemented in `task-workflow-board` via `submitCreateTask()`.
- [OK] Closing a task from the UI is implemented in `task-workflow-board` via `submitCloseTask()`.
- [OK] Default current user wiring is implemented in `TaskWorkflowBoardComponent` (`DEFAULT_CURRENT_USER_ID = 1` + `setCurrentUserId` on init).
- [OK] Strict mode is explicitly configured in `frontend/tsconfig.json` (`"strict": true`).

## Task Workflow UI Contract

The task workflow UI is intentionally aligned to the backend Procurement and
Development workflow contract. Verify changes against:

- `src/app/tasks/task-workflow-board.component.ts`
- `src/app/tasks/task.interfaces.ts`
- `src/app/tasks/task.service.ts`
- `src/app/tasks/task-workflow-adapters.ts`
- `src/app/tasks/procurement-fields.component.*`
- `src/app/tasks/development-fields.component.*`

### Supported task types and statuses

- Supported UI task types are `Procurement` and `Development`.
- New tasks start at status `1` (`TASK_STATUS.CREATED`).
- Closed tasks use status `99`.
- Procurement final status is `3`.
- Development final status is `4`.
- Generic labels are `Created`, `Status 2`, `Status 3`, `Status 4`, and
  `Closed`; type-specific meaning comes from the field components and backend
  metadata.

### API payloads

Create requests use:

```ts
{
  taskType: 'Procurement',
  description: 'Collect supplier quotes',
  assignedToUserId: 1,
  customFields?: {}
}
```

Status-change requests use:

```ts
{
  newStatus: 2,
  nextAssignedToUserId: 2,
  customFields: {
    prices: ['5000', '4800']
  }
}
```

Do not send `newDataJson` or `customDataJson` from the frontend. Those names are
stale public API shapes; the backend controller accepts `customFields` and
stores it internally as JSON.

### Read behavior

`TaskService.refreshCurrentUserTasks()` calls `GET /api/tasks/user/{userId}` and
expects `PagedResult<BaseTaskDto>`. The list items are summaries and do not
include `customFields`, so `TaskWorkflowBoardComponent.selectTask()` calls
`TaskService.getTask(id)` to hydrate the selected form with detail data before
editing type-specific fields.

`TaskService.syncTaskWithState()` keeps tasks in the local signal list only when
the returned `assignedToUserId` matches the current board user. This means a
status change that reassigns the task to another user removes it from the
current user's list after the successful response.

### Field adapters

Known task types use adapters instead of raw JSON editing:

| Task type | Status | Form fields | `customFields` payload |
| --- | ---: | --- | --- |
| `Procurement` | `2` | `priceA`, `priceB` | `{ prices: [priceA, priceB] }` |
| `Procurement` | `3` | `receipt` | `{ receipt }` |
| `Development` | `2` | `specification` | `{ specification }` |
| `Development` | `3` | `branchName` | `{ branchName }` |
| `Development` | `4` | `versionNumber` | `{ versionNumber }` |

The board replaces the backend `customFields` object on each transition. If a
future workflow needs to retain previous fields, the adapter must include those
fields in the outgoing payload or the backend service must be changed to merge
data explicitly.

### Close behavior

The close button is available only when the selected task is at the final status
defined in `TASK_FINAL_STATUS_BY_TYPE`. Closing calls
`POST /api/tasks/{id}/close` with `finalNotes`; the backend sets status `99` and
adds `finalNotes`/`closedAt` to stored custom data.

### Extension pitfalls

- Adding a backend-supported task type also requires frontend updates to
  `TASK_TYPE_OPTIONS`, `TASK_FINAL_STATUS_BY_TYPE`, labels/adapters, and tests.
- Keep assignment fields numeric and greater than zero; backend validation also
  verifies that the next assignee exists.
- Keep strict TypeScript types aligned with backend DTOs:
  - list endpoints return `PagedResult<T>`
  - detail/create/status/close responses return `BaseTaskDto`-compatible task
    objects with optional `customFields`

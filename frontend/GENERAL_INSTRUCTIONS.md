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

## Task Workflow UI Contract

Source map:
- `src/app/tasks/task.interfaces.ts`: shared DTOs, status constants, and final-status mapping.
- `src/app/tasks/task.service.ts`: API calls and signal-owned task state.
- `src/app/tasks/task-workflow-board.component.ts`: create, change-status, rollback, and close flows.
- `src/app/tasks/task-workflow-adapters.ts`: task-type payload builders for Procurement and Development.
- `src/app/tasks/procurement-fields.component.ts` and `development-fields.component.ts`: status-specific validators.

Backend-aligned constraints:
- Status `1` is the first selectable workflow status. Do not reintroduce a `0`/Backlog option in the UI.
- Status `99` is closed and is reached only through `TaskService.closeTask()`, not `changeTaskStatus()`.
- `ChangeStatusWorkflowRequest` must send:
  ```ts
  {
    newStatus: number;
    nextAssignedToUserId: number;
    customFields: Record<string, unknown>;
  }
  ```
- `CloseTaskRequest` must send both `nextAssignedToUserId` and `finalNotes`.
- The current MVP keeps reassignment stable by using the selected task's `assignedToUserId` as
  `nextAssignedToUserId` for status changes and close operations.
- `customFields` must be an object. The fallback JSON editor should reject arrays/scalars and set the
  `invalidJson` error before calling the service.

Payload examples:
```ts
// Procurement, status 2
{ prices: ['5000', '4800'] }

// Procurement, status 3
{ receipt: 'REC-001' }

// Development, status 2
{ specification: 'Detailed implementation plan' }

// Development, status 3
{ branchName: 'feature/workflow-hardening' }

// Development, status 4
{ versionNumber: '1.2.0' }
```

Common pitfalls:
- List endpoints return paged task summaries; use the returned task from workflow responses or fetch task
  details when `customDataJson` is needed.
- Keep `TASK_FINAL_STATUS_BY_TYPE` in sync with backend metadata for hard-coded task types until the UI
  consumes `/api/task-types` dynamically.
- If a future UI supports reassignment, source `nextAssignedToUserId` from an explicit user picker and keep
  the value greater than zero.

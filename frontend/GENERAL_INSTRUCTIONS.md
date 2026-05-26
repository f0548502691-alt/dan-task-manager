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

## Task Workflow Form Architecture

Use this map when changing task lifecycle forms:

- `src/app/tasks/task-workflow-board.component.ts`
  - Owns selected-task state, create/status/close forms, service calls, and status suggestions.
  - Resets dynamic controls through `resetControl` before hydrating fields for a newly selected or updated task.
  - Uses `takeUntilDestroyed(this.destroyRef)` for create/status/close subscriptions.
- `src/app/tasks/task-workflow-adapters.ts`
  - Maps a task type to the form hydration and payload-building rules for status changes.
  - Keep task-type-specific `customFields` shapes here instead of adding `if (taskType === ...)` branches to the board.
- `src/app/tasks/task-form.utils.ts`
  - `syncControlState` enables a control with validators or clears and resets it when disabled.
  - `resetControl` clears value, errors, dirty/touched state, and suppresses value-change events.
  - `parseTaskCustomDataJson` accepts empty JSON as `{}`; invalid JSON returns `{ data: {}, isValid: false }`.
- `procurement-fields` and `development-fields`
  - Render status-specific fields and validators only.
  - Reference `TASK_STATUS` constants in templates instead of raw status numbers.

### Built-in task form adapters

| Task type | Status | Form fields | Payload sent as task custom data |
|-----------|--------|-------------|----------------------------------|
| `Procurement` | `TASK_STATUS.READY_FOR_REVIEW` (`2`) | `priceA`, `priceB` | `{ "prices": ["5000", "4800"] }` |
| `Procurement` | `TASK_STATUS.DONE` (`3`) | `receipt` | `{ "receipt": "REC-001" }` |
| `Development` | `TASK_STATUS.READY_FOR_REVIEW` (`2`) | `specification` | `{ "specification": "Implementation plan..." }` |
| `Development` | `TASK_STATUS.DONE` (`3`) | `branchName` | `{ "branchName": "feature/task-workflow" }` |
| `Development` | `TASK_STATUS.RELEASED` (`4`) | `versionNumber` | `{ "versionNumber": "1.2.0" }` |

Task types without an adapter use the `fallbackJson` control. The fallback must parse to a JSON object; arrays, primitives, and invalid JSON are not accepted as task custom data.

### Adding a new task form

1. Add status constants or labels in `task.interfaces.ts` only if the workflow introduces new statuses.
2. Add a focused field component when the task type has dedicated UI fields and validators.
3. Add a `TaskWorkflowAdapter` entry that:
   - Hydrates controls from `task.customDataJson`.
   - Builds the exact `customFields` object expected by backend validation for each status.
4. Keep the board component responsible for orchestration only; do not move service calls into field components.

Backend constraints still apply: status changes must move forward by exactly one status or backward to a lower status, and closed tasks use status `99`.

# Frontend General Instructions

This file is the default decision guide for client-side architecture choices.

## Current task workflow architecture

The task workflow UI is intentionally feature-local:

- `src/app/tasks/task.service.ts` owns API-backed task state with Angular `signal`s:
  - `currentUserId`, `tasks`, `isLoading`, and `error` are exposed as read-only signals.
  - `taskCount` and `hasTasks` are simple `computed` derivations.
  - Mutations go through service methods that call `/api/tasks`, `/api/tasks/user/{userId}`,
    `/api/tasks/{id}/change-status`, `/api/tasks/{id}/close`, `/api/tasks/{id}`, and DELETE `/api/tasks/{id}`.
- `src/app/tasks/task-workflow-board.component.ts` owns screen-local state:
  - selected task, submit-in-flight state, success messages, and the reactive form.
  - workflow payloads are built from form controls before calling `TaskService.changeTaskStatus`.
- `src/app/tasks/procurement-fields.component.ts` and
  `src/app/tasks/development-fields.component.ts` only manage validators for type-specific form fields.
- `src/app/tasks/task.interfaces.ts` is the frontend contract for status constants, task DTOs, and workflow request/response shapes.

Keep this ownership split unless state begins crossing feature boundaries. The backend remains the source of truth for workflow validation; the frontend should guide users and format payloads, not duplicate final authorization or transition rules.

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

### Examples

Stay with service Signals when:

- A single feature screen consumes the state.
- Derived values are simple counts, booleans, or labels.
- Async flows are thin wrappers around one API call and one local state update.
- Tests can exercise behavior through service methods without complex setup.

Consider Signal Store when:

- Dashboards, task lists, notifications, and workflow editors all need the same task cache.
- Multiple components repeat filtering, grouping, or computed status derivations.
- Optimistic updates, rollback, or request deduplication becomes shared behavior.
- The same loading/error/update patterns are copied across several services.

## Migration constraints if Signal Store is adopted

If a future change crosses the checklist threshold, migrate deliberately:

1. Preserve the public task contract in `task.interfaces.ts`; update it alongside backend DTO changes.
2. Keep API side effects centralized. Store methods or effects should still call the same task endpoints through one API boundary.
3. Preserve read-only consumption from components. Components should not patch task arrays directly.
4. Keep workflow validation on the backend. Frontend logic may enable fields and build JSON payloads, but server responses decide whether a transition succeeds.
5. Maintain current task-list semantics unless the product changes them:
   - closed tasks use status `99` and are removed from the open task list;
   - visible tasks are sorted by newest `createdAt` first;
   - task-type-specific payload keys are `prices`, `receipt`, `specification`, `branchName`, and `versionNumber`.
6. Before migrating, check that `/api/tasks/user/{userId}` response shape still matches the frontend task DTO needs, including `customDataJson` for form hydration.

## Current project decision
- For the current task workflow UI, existing Angular Signals in `TaskService` are sufficient.
- No immediate migration to Signal Store is required.

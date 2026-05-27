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

## Global Error Handling

### Backend contract

The backend now normalizes handled API errors through `GlobalExceptionMiddleware`:

```json
{
  "error": "TaskType נדרש",
  "code": "validation_failed"
}
```

- Display `error` to users.
- Treat `code` as optional. It is useful for future branching or telemetry, but the UI should not fail if it is absent.
- Do not parse localized `error` text for business logic.

### Client flow

The root bootstrap in `src/main.ts` wires the error stack:

```ts
provideHttpClient(withInterceptors([httpErrorInterceptor])),
{ provide: ErrorHandler, useClass: AppErrorHandler },
provideZonelessChangeDetection()
```

Codepaths:

- `src/app/core/error-message.utils.ts` extracts user-facing messages. It prefers backend `{ error, code? }`, then string payloads, then `HttpErrorResponse.message`, then fallback copy.
- `src/app/core/http-error.interceptor.ts` writes HTTP failures to `AppErrorService` and rethrows `Error(message)`.
- `src/app/core/app-error-handler.ts` catches unhandled client-side errors, stores the extracted message globally, and logs the original error.
- `src/app/core/app-error.service.ts` owns the global signal.
- `src/app/app.component.ts` renders the global banner from `AppErrorService.error()`.
- `TaskService` still owns feature-level task errors. Each task API method clears both local and global errors before a request, then stores failures in both places.

### Error-handling constraints

- Keep cross-cutting HTTP parsing in `error-message.utils.ts`; components should not inspect `HttpErrorResponse` directly.
- Preserve the interceptor's `throwError(() => new Error(message))` behavior so subscribers receive a plain `Error` with the displayed message.
- When adding a new service method, call `clearErrorsState()` before the request and route failures through `handleHttpError()`.
- Keep the app zoneless-friendly: use signals, observables, or explicit state updates for async error UI.

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

## Task API Boundary Notes

The backend public request models live under `backend/Contracts/Requests` and use `customFields` for dynamic task data.

- Status changes send `ChangeStatusWorkflowRequest` with `newStatus`, `nextAssignedToUserId`, and a required `customFields` object.
- Close requests send `CloseTaskRequest` with `nextAssignedToUserId` and `finalNotes`.
- List endpoints return `PagedResult<TaskSummaryDto>` and omit `customFields`; detail/change/close responses include task details.
- `TaskService.normalizeTaskCollection()` accepts both paged results and legacy arrays so the board remains tolerant while the API contract settles.
- `TaskService.extractCustomDataJson()` accepts either backend `customFields` objects or legacy `customDataJson` strings and stores the normalized string for existing form hydration helpers.

Do not introduce new `newDataJson` payloads in the client. If create-task dynamic fields are added to the UI, align the TypeScript request type with the backend `customFields` contract instead of extending `customDataJson`.

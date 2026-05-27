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

## Angular Bootstrap and Runtime

The frontend is a standalone Angular application with zoneless change detection. Keep the application shell wired through `frontend/src/main.ts`:

```ts
bootstrapApplication(AppComponent, {
  providers: [provideHttpClient(), provideZonelessChangeDetection()]
});
```

### Intent
- `bootstrapApplication` owns root startup; `AppComponent` is standalone and imports `TaskWorkflowBoardComponent` directly.
- `provideHttpClient()` is registered once at bootstrap for services such as `TaskService`.
- `provideZonelessChangeDetection()` removes the Zone.js runtime requirement and matches the existing signal-driven, `OnPush` component architecture.

### Build and dependency constraints
- `frontend/angular.json` points the browser entry to `src/main.ts` and keeps `polyfills` empty.
- `frontend/package.json` should not include `zone.js` or `@angular/platform-browser-dynamic` for normal application startup.
- Add new UI capabilities as standalone components/providers. Do not introduce an `AppModule`/`BrowserModule` shell unless intentionally reversing the standalone architecture.

### Zoneless update guidance
- Template-affecting async state should flow through Angular-aware primitives: signals/computed values, Reactive Forms controls, HttpClient streams that update signals, or explicit change detector APIs for non-Angular callbacks.
- Browser timers, DOM APIs, and third-party callbacks are not Zone.js-patched. If one mutates component state, write to a signal or call `ChangeDetectorRef.markForCheck()` after the mutation.
- Keep component subscriptions scoped with `takeUntilDestroyed(this.destroyRef)`.

Example:

```ts
someExternalCallback((message) => {
  this.successMessage.set(message);
});
```

### Setup and troubleshooting
- Use `npm --prefix frontend start` for local development; it runs `ng serve --proxy-config proxy.conf.json` and forwards `/api` to `http://localhost:8080`.
- Use `npm --prefix frontend build` to verify the standalone zoneless bundle.
- If a build or runtime change expects `zone.js`, first check whether the integration can be expressed with signals, reactive forms, or explicit change detection. Reintroducing Zone.js changes the runtime contract and should be a deliberate architecture decision.

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
- [OK] Standalone zoneless bootstrap is configured in `src/main.ts`; `angular.json` has no Zone.js polyfill and `package.json` has no Zone.js dependency.

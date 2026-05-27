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

## Angular Workspace Scaffold

The frontend is a standalone Angular application rooted at `frontend/`.

### Entrypoints and runtime wiring

- `src/index.html` hosts the app through `<app-root></app-root>`.
- `src/main.ts` bootstraps the standalone `AppComponent` with `provideHttpClient()`.
- `src/app/app.component.ts` is intentionally thin. It renders the app shell and imports
  `TaskWorkflowBoardComponent`.
- Task workflow behavior remains under `src/app/tasks/`; avoid moving workflow state or
  API calls into `AppComponent`.

### Package scripts

Run commands from `frontend/`:

```bash
npm install
npm start
npm run build
npm test
```

- `npm start` runs `ng serve --proxy-config proxy.conf.json`.
- `npm run build` runs the Angular production build and writes to `dist/frontend`.
- `npm test` is a placeholder that prints that no frontend test runner is configured yet.
  Do not treat it as executable test coverage.

### Local backend proxy

`proxy.conf.json` forwards browser calls from `/api` to `http://localhost:8080`.
This keeps client code using relative URLs such as `/api/tasks` and `/api/task-types`.
When developing locally, start the backend on port `8080` or update the proxy target
for your environment.

### Angular build configuration

- `angular.json` defines a single application project named `frontend`.
- The app uses the `@angular/build:application` builder with `src/main.ts` as the
  browser entrypoint and `src/styles.css` as the global stylesheet.
- The default build configuration is `production`; `ng serve` defaults to
  `development`.
- Production budgets are intentionally small (`500kB` warning / `1MB` error initial
  bundle, `4kB` warning / `8kB` error component styles). Keep new dependencies and
  component styles lean unless the budgets are deliberately revisited.

### TypeScript constraints

`tsconfig.json` keeps strict TypeScript and Angular template checking enabled:

- `"strict": true`
- `"noImplicitOverride": true`
- `"noPropertyAccessFromIndexSignature": true`
- `"noImplicitReturns": true`
- `"noFallthroughCasesInSwitch": true`
- `"strictTemplates": true`

Use explicit types for API shapes and form payloads. Access dynamic object keys with
bracket notation, as required by `noPropertyAccessFromIndexSignature`.

### Generated and build artifacts

The frontend `.gitignore` excludes Angular cache, build output, dependencies, and
TypeScript output:

- `.angular/`
- `dist/`
- `node_modules/`
- `out-tsc/`

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
- [OK] The Angular CLI workspace is present (`angular.json`, `package.json`, `package-lock.json`, and `proxy.conf.json`).

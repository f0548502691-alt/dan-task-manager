# Dan Task Manager frontend

Angular workflow board for the Dan Task Manager backend. The client is a
standalone, zoneless Angular app that talks to the backend through `/api`.

## Setup

From `frontend/`:

```bash
npm install
npm start
```

`npm start` runs `ng serve --proxy-config proxy.conf.json`; the proxy forwards
`/api` calls to `http://localhost:8080`. Use `docker compose up --build` from the
repository root to start the backend and SQL Server.

Available scripts:

| Script | Purpose |
|--------|---------|
| `npm start` | Serve the app with the `/api` proxy |
| `npm run build` | Production build |
| `npm run watch` | Development build watch mode |
| `npm test` | Placeholder script; no test runner is configured yet |

## Bootstrap and change detection

`src/main.ts` bootstraps `AppComponent` with:

- `provideHttpClient(withInterceptors([httpErrorInterceptor]))`
- `{ provide: ErrorHandler, useClass: AppErrorHandler }`
- `provideZonelessChangeDetection()`

Do not add `zone.js` assumptions to new UI code. Prefer signals, reactive forms,
RxJS subscriptions cleaned up with `takeUntilDestroyed`, or explicit state
updates that Angular can observe in zoneless mode.

## Task workflow UI

Primary files:

| File | Responsibility |
|------|----------------|
| `src/app/tasks/task.service.ts` | API calls, signal-owned current user task list, response normalization |
| `src/app/tasks/task-workflow-board.component.ts` | Create/status/close forms and selection state |
| `src/app/tasks/task.interfaces.ts` | Client-side API contracts and status constants |
| `src/app/tasks/task-workflow-adapters.ts` | Type-specific custom field builders/hydrators |
| `src/app/tasks/procurement-fields.component.*` | Procurement form fields |
| `src/app/tasks/development-fields.component.*` | Development form fields |
| `src/app/core/*` | Global API and runtime error handling |

The board defaults to user `1`, loads `/api/task-types`, filters inactive or
empty task type entries, and falls back to `Procurement` and `Development` when
metadata cannot be loaded.

## API contract used by the client

The current public backend contract uses `customFields`:

```ts
interface CreateTaskRequest {
  taskType: string;
  description: string;
  assignedToUserId: number;
  customFields?: Record<string, unknown>;
}

interface ChangeStatusWorkflowRequest {
  newStatus: number;
  nextAssignedToUserId: number;
  customFields: Record<string, unknown>;
}
```

Important constraints:

- Created status is `1`; closed status is `99`.
- List endpoints return a paged object with `items`.
- List task summaries do not include `customFields`.
- The board fetches `GET /api/tasks/{id}` when a task is selected so forms can
  hydrate from detail data.
- `TaskService` still tolerates legacy array list responses and legacy
  `customDataJson` strings when normalizing responses, but new backend requests
  should use `customFields`.

## Type-specific payloads

| Task type | Status | Payload |
|-----------|--------|---------|
| Procurement | 2 | `{ "prices": ["5000", "4800"] }` |
| Procurement | 3 | `{ "receipt": "REC-123" }` |
| Development | 2 | `{ "specification": "At least ten characters" }` |
| Development | 3 | `{ "branchName": "feature/task-10" }` |
| Development | 4 | `{ "versionNumber": "1.0.0" }` |

Known task types use adapters and dedicated form components. Unknown metadata
types use the fallback JSON editor and must submit a valid JSON object.

## Error handling

`httpErrorInterceptor` extracts backend `{ error, code }` responses and pushes a
message into `AppErrorService`. Component subscriptions intentionally use empty
error callbacks because the global service owns display state.

When debugging UI errors:

1. Confirm the backend is reachable at `http://localhost:8080`.
2. Confirm `/api/task-types` returns active schemas or the fallback types will be
   used.
3. Confirm status requests include `nextAssignedToUserId` and object-shaped
   `customFields`.
4. Fetch task details if a list row appears to have missing custom field data.

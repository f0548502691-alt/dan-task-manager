# Frontend task workflow guide

The Angular client is a standalone, zoneless app that renders the task workflow
board from backend task-type metadata. Use this guide when changing workflow UI,
status labels, or dynamic fields.

## Run the client

```bash
npm install
npm start
```

`npm start` serves the app with `proxy.conf.json`, forwarding `/api` to
`http://localhost:8080`. In Docker, `npm run start:docker` uses
`proxy.docker.conf.json`, forwarding `/api` to `http://backend:8080`.

## Source map

| Area | Codepath | Notes |
|------|----------|-------|
| Angular bootstrap | `src/main.ts` | Registers `provideHttpClient(withInterceptors([httpErrorInterceptor]))`, `AppErrorHandler`, and `provideZonelessChangeDetection()`. |
| Workflow board | `src/app/tasks/task-workflow-board.component.*` | Owns create, select, status-change, and close forms. Uses Angular signals for UI state. |
| API/state service | `src/app/tasks/task.service.ts` | Calls `/api/tasks`, `/api/tasks/{id}`, `/api/tasks/{id}/change-status`, `/api/tasks/{id}/close`, and `/api/task-types`; normalizes paged and legacy task-list responses. |
| Shared task types | `src/app/tasks/task.interfaces.ts` | Defines DTOs, `CREATED_TASK_STATUS = 1`, `CLOSED_TASK_STATUS = 99`, and fallback status labels. |
| Dynamic fields | `src/app/tasks/dynamic-task-fields.component.ts`, `task-schema.utils.ts` | Builds reactive controls from backend field metadata for the selected next status. |
| Form helpers | `src/app/tasks/task-form.utils.ts` | Parses fallback JSON and resets controls consistently. |

## Workflow data flow

1. `TaskWorkflowBoardComponent.ngOnInit()` sets the demo current user to `1` and
   asks `TaskService` to load that user's tasks.
2. The board loads task-type metadata from `GET /api/task-types` and stores
   schemas by `taskType`.
3. Selecting a task refreshes schema context, sets the suggested next status, and
   hydrates details from `GET /api/tasks/{id}` so dynamic fields can use current
   `customFields`.
4. Status updates submit:

   ```json
   {
     "newStatus": 2,
     "nextAssignedToUserId": 1,
     "customFields": { "prices": ["5000", "4800"] }
   }
   ```

5. Closing uses the dedicated endpoint and payload:

   ```json
   {
     "nextAssignedToUserId": 1,
     "finalNotes": "Ready to close"
   }
   ```

The backend owns workflow validation. The frontend mirrors enough metadata to
render fields and client-side validators, but it should not invent extra status
rules.

## Status labels and dropdown options

Status values remain numeric workflow states. The frontend exposes two constants:

- `CREATED_TASK_STATUS = 1`
- `CLOSED_TASK_STATUS = 99`

Fallback display labels are uppercase:

```ts
DEFAULT_STATUS_LABELS = {
  [TASK_STATUS.CREATED]: 'CREATED',
  [TASK_STATUS.CLOSED]: 'CLOSED'
};
```

For statuses without a fallback label, the board displays `Status N`.

The status dropdown is generated from status `1` through
`max(task.currentStatus, finalStatus)`. The option value is always the numeric
status that will be submitted in `ChangeStatusWorkflowRequest.newStatus`.

Special case: when backend metadata says a task type has exactly two workflow
states (`finalStatus === 2`), the dropdown displays status `2` as `CLOSED`.
This is a UI label only:

```text
value submitted: 2
label shown:     CLOSED
```

The hard closed state is still status `99` and is reachable only through the
`POST /api/tasks/{id}/close` flow. Do not treat the two-state dropdown label as a
replacement for `CLOSED_TASK_STATUS`.

## Dynamic field constraints

Dynamic controls are built from `TaskFieldRuleDto`:

- `appliesFromStatus` / `appliesToStatus` decide which fields show for the
  selected next status.
- `array` rules render fixed scalar arrays using `arrayLength`, `minItems`,
  or `maxItems` as the item count fallback.
- `allowedValues` render a select control.
- `string` rules with long limits or minimum length at least `10` render a
  textarea.
- Unknown or schema-less task types fall back to editable JSON.

When adding metadata-backed task types, verify that the status labels users see
match the numeric `finalStatus` and that the backend still accepts the generated
`customFields` payload.

## Common pitfalls

- Parent component CSS does not style child component internals through Angular
  view encapsulation; update the child component stylesheet when changing dynamic
  field layout.
- `localhost` inside the frontend Docker container is not the backend container;
  use `proxy.docker.conf.json` for container-to-container API calls.
- The client is zoneless. Prefer signals, reactive forms, or explicit state
  updates after async work.
- Keep status labels separate from persisted status values. Changing
  `DEFAULT_STATUS_LABELS` affects UI text only.

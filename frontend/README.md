# Frontend task workflow guide

The Angular client is a standalone task workflow board for the backend API. Use
this guide when changing workflow UI, status labels, dynamic fields, or local
development setup.

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
| Bootstrap | `src/main.ts` | Starts the standalone Angular app and registers HTTP/error providers. |
| Workflow board | `src/app/tasks/task-workflow-board.component.*` | Owns create, task selection, status-change, and close forms. Uses signals for UI state and computed status options. |
| API/state service | `src/app/tasks/task.service.ts` | Calls `/api/tasks`, `/api/tasks/{id}`, `/api/tasks/{id}/change-status`, `/api/tasks/{id}/close`, and `/api/task-types`; normalizes paged and legacy task-list responses. |
| DTOs and constants | `src/app/tasks/task.interfaces.ts` | Defines request/response shapes, status constants, default labels, and known task-type label maps. |
| Dynamic fields | `src/app/tasks/dynamic-task-fields.component.*`, `task-schema.utils.ts` | Builds reactive controls from backend field metadata for the selected next status. |
| Form helpers | `src/app/tasks/task-form.utils.ts` | Parses fallback JSON payloads and resets controls. |

## Workflow data flow

1. `TaskWorkflowBoardComponent.ngOnInit()` sets the demo current user to `1`.
   `TaskService.setCurrentUserId()` then loads that user's task list from
   `GET /api/tasks/user/{id}`.
2. The board loads active task-type schemas from `GET /api/task-types`.
   `TaskService` caches them by `taskType` for label resolution, final-status
   lookup, and dynamic-field rendering.
3. Selecting a task sets the suggested next status, refreshes schema context,
   and loads details from `GET /api/tasks/{id}` so current `customFields` can
   hydrate the form.
4. Status updates submit numeric workflow state plus custom fields:

   ```json
   {
     "newStatus": 2,
     "nextAssignedToUserId": 1,
     "customFields": { "prices": ["5000", "4800"] }
   }
   ```

5. Closing is separate from normal status movement and uses:

   ```json
   {
     "nextAssignedToUserId": 1,
     "finalNotes": "Ready to close"
   }
   ```

The backend owns workflow validation. The frontend renders controls and simple
validators from metadata, but it should not invent additional movement rules.

## Status values and labels

Status values are persisted and submitted as numbers. The shared constants are:

```ts
CREATED_TASK_STATUS = 1;
CLOSED_TASK_STATUS = 99;
```

The UI resolves display text in this order:

1. `DEFAULT_STATUS_LABELS` for global values (`1` -> `Created`,
   `99` -> `Closed`).
2. `TASK_TYPE_STATUS_LABELS` for known task-type/status pairs. Matching is
   case-insensitive by task type.
3. Backend schema fields for that status. For example, a metadata status with
   `campaignName` and `targetAudience` displays as
   `Campaign Name + Target Audience`.
4. Fallback text `Status N`.

The task list and editor subtitle call `taskStatusLabel(task)`, so they show the
label for the task's current numeric status. The status dropdown stores numeric
option values and only changes the text users see.

### Current built-in labels

| Task type | Status labels |
|-----------|---------------|
| Any task | `1` = `Created`, `99` = `Closed` |
| Procurement | `2` = `Quotes collected`, `3` = `Receipt received` |
| Development | `2` = `Specification ready`, `3` = `Branch ready`, `4` = `Version ready` |
| Marketing | `2` = `Campaign details`, `3` = `Launch scheduled` |
| Analysis | `2` = `Analysis complete` |
| Testing | `2` = `Test cases defined`, `3` = `Testing complete` |

If a task type has metadata but no explicit entry in `TASK_TYPE_STATUS_LABELS`,
the label falls back to the applicable field names for that status. If there are
no fields and the status is the final status, the dropdown displays
`Ready to close`; otherwise it displays `Status N`.

Important constraint: `Closed` means the hard closed status `99`. A task reaches
that state only through `POST /api/tasks/{id}/close`; normal status updates must
not submit `99`.

## Dynamic field behavior

`task-schema.utils.ts` filters field rules by `appliesFromStatus` /
`appliesToStatus` for the selected next status, then chooses controls from the
rule shape:

- `array` renders a fixed scalar array using `arrayLength`, `minItems`, or
  `maxItems` as the item count fallback.
- `allowedValues` renders a select control.
- `boolean` renders a checkbox.
- numeric types render number inputs.
- long string fields render textareas.
- schema-less task types use the fallback JSON editor.

When adding a backend task type, verify both the generated `customFields`
payload and the status label users will see. Add an explicit
`TASK_TYPE_STATUS_LABELS` entry when field-derived labels are too technical for
operators.

## Common pitfalls

- Labels are client-side display text only; backend APIs still expect numeric
  `newStatus` and enforce all movement rules.
- The status dropdown includes values from `1` through the larger of the current
  status and the task type's `finalStatus`; invalid same-status or skipped
  forward moves are still rejected by the backend.
- `localhost` inside the frontend Docker container is not the backend container;
  use `proxy.docker.conf.json` for Docker.
- Parent component CSS does not style child dynamic-field DOM through Angular
  view encapsulation; update the child component stylesheet for field layout.

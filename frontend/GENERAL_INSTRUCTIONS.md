# Frontend General Instructions

This file is the default decision guide for client-side architecture choices in
the Angular task workflow UI.

## State management policy

### Current default

- Use Angular Signals in services/components for small-to-medium scoped state.
- Do not add Signal Store by default.
- Keep API state ownership in `TaskService`; keep form-local state in workflow
  components.

### When Signal Store is needed

Adopt Signal Store when at least one of these is true:

1. State is shared across multiple unrelated feature areas.
2. Update logic becomes duplicated across several services/components.
3. Centralized updaters/effects are needed for async flows.
4. Selectors/computed derivations become complex and repetitive.
5. Testing state transitions is difficult with the current structure.

If 0-1 items are true, stay with service Signals. If 2+ items are true, move to
Signal Store.

## Angular baseline

- Standalone components remain the default.
- Keep TypeScript strict-mode compatible.
- Prefer reactive forms for workflow inputs.
- Keep templates focused and move reusable task-field rendering into dedicated
  components.
- The MVP still uses a hard-coded current user ID (`DEFAULT_CURRENT_USER_ID = 1`).

## Current task workflow source map

| Concern | File |
|---------|------|
| Task API calls, task list signal state, response normalization | `src/app/tasks/task.service.ts` |
| Create/status/close forms and selected-task workflow | `src/app/tasks/task-workflow-board.component.ts` |
| Board template and fallback JSON editor | `src/app/tasks/task-workflow-board.component.html` |
| Dynamic metadata-backed field rendering | `src/app/tasks/task-dynamic-fields.component.ts` and `.html` |
| Schema helpers, validators, payload building, fallback schemas | `src/app/tasks/task-workflow-schema.ts` |
| DTO and request interfaces | `src/app/tasks/task.interfaces.ts` |
| JSON parsing and form reset helpers | `src/app/tasks/task-form.utils.ts` |

## Task type metadata flow

On initialization, the board calls `TaskService.getTaskTypes()`:

```text
GET /api/task-types
  -> filter active schemas with non-empty taskType
  -> sort by displayName/taskType
  -> store in taskTypeSchemas signal
  -> select the first option for create form when none is selected
```

If the request fails or returns no schemas, the UI uses
`FALLBACK_TASK_TYPE_SCHEMAS` for `Procurement` and `Development`. The fallback is
only a development safety net; the backend catalog is the source of truth.

## Dynamic status fields

`TaskDynamicFieldsComponent` renders fields for the selected task type and target
status. `task-workflow-schema.ts` decides applicability:

- A field is shown when the selected status is between `appliesFromStatus` and
  `appliesToStatus` inclusive.
- Required/min/max/pattern validators are applied only to active controls.
- Inactive controls are reset and have validators cleared.
- Array fields render a fixed number of controls using `arrayLength`, then
  `minItems`, then `maxItems`, then `1` as fallback.
- Numeric and boolean payload values are coerced before submission where the
  schema type makes that possible.

Supported client render types today:

| Schema type | UI behavior |
|-------------|-------------|
| `string` | Text input, or textarea when long/specification-like. |
| `number`, `integer`, `decimal` | Number input and numeric payload coercion. |
| `stringOrNumber` | Text input; backend validates version/pattern semantics. |
| `array` | Fixed count of inputs; values are sent as an array. |
| Unknown / no fields | Fallback JSON editor. |

The backend still performs authoritative validation. Client validators are for
fast feedback only.

## Workflow payloads

Create requests send only the selected type, description, and assignee unless a
future create schema adds fields:

```json
{
  "taskType": "Procurement",
  "description": "Collect supplier quotes",
  "assignedToUserId": 1
}
```

Status changes always send public `customFields` and `nextAssignedToUserId`:

```json
{
  "newStatus": 2,
  "nextAssignedToUserId": 2,
  "customFields": {
    "prices": ["5000", "4800"]
  }
}
```

When a task type has no schema fields for the target status, the fallback JSON
editor is hydrated from task details and parsed with `parseTaskCustomDataJson()`.
Invalid JSON sets the `invalidJson` form error and blocks submission.

## Response normalization

`TaskService` accepts the current paged list response and older array responses:

- `GET /api/tasks/user/{id}` may return `PagedResultDto<unknown>` with `items`.
- Legacy array payloads are still normalized defensively.
- List DTOs may omit `customFields`; task detail reads hydrate the selected task.
- `customFields` is preferred; `customDataJson` is parsed only as a defensive
  fallback for older payloads.

## Extension checklist

When backend task type metadata changes:

1. Verify `GET /api/task-types` exposes the schema the UI needs.
2. Keep status ranges narrow so fields are required only on the intended target
   status.
3. Add client rendering support before relying on new field types or variable
   length arrays.
4. Keep `TaskService` DTOs aligned with public backend names (`customFields`,
   `nextAssignedToUserId`).
5. Run `npm --prefix frontend test` and, when dependencies are installed,
   `npm --prefix frontend run build`.

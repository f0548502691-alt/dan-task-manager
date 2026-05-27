# Frontend task workflow guide

This Angular app is a small workflow board for the Dan Task Manager API. It is
standalone, zoneless, and intentionally keeps task-type-specific fields out of
Angular components. The backend task type schema decides which fields appear for
each status transition.

## Run and verify

```bash
npm --prefix frontend ci
npm --prefix frontend start
```

- `npm start` runs `ng serve --proxy-config proxy.conf.json`.
- `/api/*` is proxied to `http://localhost:8080`; run the backend there or
  adjust `frontend/proxy.conf.json`.
- `npm --prefix frontend run build` performs the production Angular build.
- `npm --prefix frontend test` is currently a placeholder script and does not
  run a browser test runner.

## Architecture map

| File | Responsibility |
| --- | --- |
| `src/main.ts` | Bootstraps `AppComponent` with `provideHttpClient(withInterceptors([httpErrorInterceptor]))`, `AppErrorHandler`, and zoneless change detection. |
| `src/app/app.component.ts` | App shell, global error display, and `TaskWorkflowBoardComponent` host. |
| `src/app/tasks/task.service.ts` | API access, signal-owned task state, schema cache, task response normalization, and newest-first sorting. |
| `src/app/tasks/task-workflow-board.component.ts` | Create/select/update/close workflow orchestration and form ownership. |
| `src/app/tasks/dynamic-task-fields.component.ts` | Generic renderer for schema fields; emits the resolved fields used to build status-change payloads. |
| `src/app/tasks/task-schema.utils.ts` | Field applicability, control construction, validators, hydration, and payload coercion. |
| `src/app/core/*` | Shared HTTP/global error handling. |

## Backend schema contract

On startup the board loads:

```http
GET /api/task-types
```

The response is expected to match `TaskTypeSchemaDto` from
`frontend/src/app/tasks/task.interfaces.ts` and the backend
`TaskTypeValidationService`:

```json
[
  {
    "taskType": "Development",
    "displayName": "Development",
    "finalStatus": 4,
    "isActive": true,
    "version": 1,
    "fields": [
      {
        "field": "specification",
        "type": "string",
        "required": true,
        "minLength": 10,
        "appliesFromStatus": 2,
        "appliesToStatus": 2
      }
    ]
  }
]
```

The frontend filters out inactive entries and entries with blank `taskType`,
sorts the rest by task type, and caches schemas by `taskType`. The board uses
`finalStatus` to build available status options; if a schema is missing, it uses
the task's current status as the fallback final status. Status options start at
created status `1`; closed tasks use status `99`.

Backend codepaths that define this contract:

- `backend/Controllers/TaskTypesController.cs`
- `backend/Services/TaskTypeValidationService.cs`
- `backend/Data/HybridSchemaBootstrapper.cs`
- `backend/Domain/TaskTypeMetadata.cs`

## Field rendering rules

`DynamicTaskFieldsComponent` rebuilds the `customFields` form group whenever the
schema, target status, or owning form group changes. A field is shown only when:

```text
appliesFromStatus <= selected status <= appliesToStatus
```

Missing bounds are treated as open-ended. Empty or missing `fields` causes the
board to fall back to a raw JSON textarea.

| Schema rule | Angular control |
| --- | --- |
| `type: "array"` | Fixed-size `FormArray` of scalar inputs. Item count is `arrayLength`, then `minItems`, then `maxItems`, then `1`. |
| `type: "boolean"` | Checkbox. |
| `allowedValues` present | Select dropdown. |
| `type: "number"`, `"integer"`, or `"decimal"` | Number input with min/max validators when provided. |
| `type: "string"` with `maxLength > 120` or `minLength >= 10` | Textarea. |
| Any other scalar | Text input. |

Client validators mirror the schema fields for `required`, `minLength`,
`maxLength`, `minValue`, `maxValue`, and `pattern`. `allowedValues` is presented
as a select control, while the backend remains the source of truth for workflow
validation.

## Workflow payloads

Create uses the selected task type, description, and assigned user:

```json
{
  "taskType": "Procurement",
  "description": "Buy laptop docks",
  "assignedToUserId": 1
}
```

Status changes send the selected status, next assignee, and a `customFields`
object built from the schema fields applicable to that target status:

```json
{
  "newStatus": 2,
  "nextAssignedToUserId": 2,
  "customFields": {
    "prices": ["5000", "4800"]
  }
}
```

Closing a task is enabled when the task is at its cached schema `finalStatus`
(or the current status when no schema is cached):

```json
{
  "nextAssignedToUserId": 2,
  "finalNotes": "Receipt attached and approved."
}
```

The task list endpoint may return summaries without `customFields`. Selecting a
task calls `GET /api/tasks/{id}` and rehydrates the dynamic fields from the
detail response.

## Extending task fields safely

1. Add or update backend task type metadata through the task-type APIs or seed
   data. The built-in metadata seeds:
   - Procurement: `prices` at status 2, `receipt` at status 3.
   - Development: `specification` at status 2, `branchName` at status 3,
     `versionNumber` at status 4.
2. Keep `appliesFromStatus` and `appliesToStatus` within the task type's
   `finalStatus`; backend metadata upserts reject ranges outside the workflow.
3. Prefer schema changes over new Angular components. Add frontend code only
   when a new control kind or custom client-side validator is required.
4. If you introduce backend named patterns such as `valid_git_branch` or
   `semantic_version`, update `task-schema.utils.ts` before relying on matching
   client-side validation. The generic renderer currently treats `pattern` as a
   JavaScript regular expression string.
5. Arrays are fixed-size in the current UI. Add dynamic add/remove controls
   before using schema rules that expect variable-length user input.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Task type dropdown is empty | Confirm `GET /api/task-types` succeeds through the proxy and returns active task types. |
| Status fields do not appear | Check `fields`, `appliesFromStatus`, `appliesToStatus`, and the selected target status. |
| Values disappear after selecting a task | Confirm `GET /api/tasks/{id}` returns `customFields`; summaries from `GET /api/tasks/user/{id}` may not include them. |
| Submit button stays disabled | Inspect the generated controls for required/min/max/pattern errors, plus `newStatus` and `nextAssignedToUserId`. |
| Raw JSON textarea appears | The selected task type has no cached schema fields; verify the schema response and `isActive` value. |

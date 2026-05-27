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

## Task Workflow UI Reference

The task workflow UI lives under `frontend/src/app/tasks` and is intentionally
small: one board component coordinates API calls and form state, while
task-type-specific components own only their visible fields and validators.

### Source map

| File | Responsibility |
|------|----------------|
| `task-workflow-board.component.ts` | Create task form, current-user task list, selected task editor, status option calculation, close flow, and task-type metadata loading. |
| `task-workflow-board.component.html` | Renders the create form, task list, status editor, type-specific field components, fallback JSON editor, and close form. |
| `task.service.ts` | API access and Signal-owned client state for current user, tasks, loading, and errors. Normalizes backend responses before updating state. |
| `task.interfaces.ts` | Shared DTOs, status constants, default labels, and fallback final statuses. |
| `task-workflow-adapters.ts` | Payload hydration/building for known task types (`Procurement`, `Development`). |
| `procurement-fields.component.ts` | Procurement-only validators and inputs. |
| `development-fields.component.ts` | Development-only validators and inputs. |
| `task-form.utils.ts` | Shared control reset, validator synchronization, and custom JSON parsing helpers. |

### Runtime flow

1. `TaskWorkflowBoardComponent.ngOnInit()` sets the MVP user ID to `1` and calls
   `TaskService.refreshCurrentUserTasks()`.
2. The board calls `TaskService.getTaskTypes()`, which reads
   `GET /api/task-types`.
3. Active task types with a non-empty `taskType` are sorted alphabetically and
   populate the create-task dropdown.
4. The board stores `finalStatus` values in `taskTypeFinalStatusMap`.
5. If metadata is unavailable or empty, the UI falls back to the built-in
   `Procurement` and `Development` options and their default final statuses.

### Metadata contract used by the UI

The UI consumes the metadata DTO shape implemented by
`backend/Controllers/TaskTypesController.cs` and
`backend/Services/TaskTypeValidationService.cs`:

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
        "field": "branchName",
        "type": "string",
        "required": true,
        "pattern": "valid_git_branch",
        "appliesFromStatus": 3,
        "appliesToStatus": 3,
        "isIndexed": true
      }
    ]
  }
]
```

Current frontend usage is deliberately limited:

- `taskType` drives the create dropdown and adapter lookup.
- `isActive` controls whether the type appears in the dropdown.
- `finalStatus` caps status options for that task type.
- `displayName`, `version`, and `fields` are typed but not rendered as dynamic
  form controls yet.

### Status and final-status rules

- `TASK_STATUS.CLOSED` is always `99`.
- Status update options start at `TASK_STATUS.IN_PROGRESS` (`1`); the backend
  rejects status values below `1`.
- Forward movement must still be exactly `+1` and backward movement may move to
  any lower status. The frontend only lists statuses up to the greater of the
  task's current status and the resolved final status.
- Known defaults remain in `DEFAULT_TASK_FINAL_STATUS_BY_TYPE`:
  - `Procurement` -> `3`
  - `Development` -> `4`
- Metadata `finalStatus` overrides those defaults when present.
- Unknown task types with no metadata `finalStatus` use the task's current
  status as the fallback cap, so the UI cannot infer a forward path by itself.

### Payload handling

Known task types use adapters instead of inline conditionals:

| Task type | Status | Submitted payload |
|-----------|--------|-------------------|
| `Procurement` | `2` / Ready for Review | `{ "prices": ["5000", "4800"] }` |
| `Procurement` | `3` / Done | `{ "receipt": "REC-123" }` |
| `Development` | `2` / Ready for Review | `{ "specification": "Detailed plan..." }` |
| `Development` | `3` / Done | `{ "branchName": "feature/workflow" }` |
| `Development` | `4` / Released | `{ "versionNumber": "1.2.0" }` |

Unknown metadata-backed task types render a raw JSON textarea. The value must
parse to a JSON object; arrays and scalars normalize to `{}` in
`parseTaskCustomDataJson`.

The backend replaces `CustomDataJson` with the submitted status payload during a
status change. If a new status needs previous fields as well as new fields, the
frontend payload builder must include the full object to preserve them.

### API response normalization

`TaskService` accepts both current and older backend shapes:

- `GET /api/tasks/user/{id}` may be a `PagedResultDto<T>` with `items` or a
  legacy array. The service normalizes either shape to `BaseTaskDto[]`.
- Task details may provide `customDataJson` as a string or `customFields` as an
  object/string. The service stores both as a `customDataJson` string for the
  board and adapters.
- Task lists are sorted newest-first by `createdAt` after normalization.
- After create, status update, or close responses, the service syncs the task
  into local state unless it is assigned to another user or has status `99`.

Backend `TaskSummaryDto` list responses currently omit custom fields. Selecting
a task loaded only from `GET /api/tasks/user/{id}` hydrates `{}` until a create,
status-change, close, or detail-shaped response provides `customFields`.

### Known integration constraints

- `TaskWorkflowBoardComponent` still hard-codes `DEFAULT_CURRENT_USER_ID = 1`.
- The frontend status-change request interface currently sends
  `newStatus` plus `newDataJson`; the backend `ChangeStatusWorkflowRequest`
  expects `newStatus`, `nextAssignedToUserId`, and `customFields`. Keep the DTOs
  aligned before relying on status updates against the current backend.
- The create request interface still exposes `customDataJson`, while the backend
  accepts `customFields`; the current create flow sends no custom data, so this
  only matters when adding create-time custom fields.
- Metadata `fields` are validation metadata, not dynamic UI rendering metadata
  yet. Add UI generation or a new adapter before expecting arbitrary task types
  to have first-class form controls.
- Add a new adapter only when the task type needs a guided form. Otherwise, the
  fallback JSON editor can submit metadata-validated payloads.

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
- [OK] Create-task task types are loaded from backend metadata when available (`TaskService.getTaskTypes()` + `GET /api/task-types`).
- [OK] Unknown task types can be edited through the fallback JSON payload field.

# Angular task workflow client

This folder contains the Angular source for the task workflow UI. The components are thin clients over the backend workflow API: the backend owns workflow validation, while the UI maps task types and statuses to the form fields needed to submit a valid `newDataJson` payload.

## Codepaths covered

| File | Responsibility |
| --- | --- |
| `src/app/tasks/task.interfaces.ts` | Shared DTOs, status constants, status labels, and final status per known task type. |
| `src/app/tasks/task.service.ts` | Signal-backed task state, HTTP calls to `/api/tasks`, error extraction, and removal of closed or unassigned tasks from local state. |
| `src/app/tasks/task-workflow-board.component.ts` | Task selection, status option calculation, form ownership, payload building, and task data hydration. |
| `src/app/tasks/procurement-fields.component.*` | Procurement-only field rendering and validators for the selected next status. |
| `src/app/tasks/development-fields.component.*` | Development-only field rendering and validators for the selected next status. |

## Workflow status contract

Use `TASK_STATUS` from `task.interfaces.ts` instead of hard-coded numbers.

| Constant | Value | Label | Notes |
| --- | ---: | --- | --- |
| `BACKLOG` | 0 | Backlog | Initial status. |
| `IN_PROGRESS` | 1 | In Progress | Generic in-progress state. |
| `READY_FOR_REVIEW` | 2 | Ready for Review | Requires task-type-specific payload for known task types. |
| `DONE` | 3 | Done | Final status for `Procurement`; intermediate status for `Development`. |
| `RELEASED` | 4 | Released | Final status for `Development`. |
| `CLOSED` | 99 | Closed | Closed tasks are removed from the local task list. |

Known final statuses live in `TASK_FINAL_STATUS_BY_TYPE`:

```ts
Procurement -> TASK_STATUS.DONE
Development -> TASK_STATUS.RELEASED
```

The UI uses this map to suggest the next status and to build the status dropdown. The backend still enforces movement rules: forward movement must advance by exactly one status, rollback can move to a lower status, the same status is rejected, and statuses beyond a handler's final status are rejected.

## Form ownership and field components

`TaskWorkflowBoardComponent` owns the complete `FormGroup` and creates every control up front:

```ts
newStatus, priceA, priceB, receipt, specification, branchName, versionNumber, fallbackJson
```

Child field components must not add or remove controls. They receive the parent form and selected next status, then only synchronize validators and clear inactive fields:

- `ProcurementFieldsComponent`
  - `READY_FOR_REVIEW`: requires `priceA` and `priceB`
  - `DONE`: requires `receipt`
- `DevelopmentFieldsComponent`
  - `READY_FOR_REVIEW`: requires `specification` with at least 10 characters
  - `DONE`: requires `branchName` with no spaces
  - `RELEASED`: requires `versionNumber`

This keeps control ownership in one place and avoids validators being attached to stale controls when users switch tasks or statuses.

## Payload examples

`submitStatusUpdate()` sends:

```json
{
  "newStatus": 2,
  "newDataJson": "{\"prices\":[\"100\",\"125\"]}"
}
```

The payload stored inside `newDataJson` depends on the target status:

| Task type | Status | Payload shape |
| --- | ---: | --- |
| `Procurement` | `READY_FOR_REVIEW` (2) | `{ "prices": ["100", "125"] }` |
| `Procurement` | `DONE` (3) | `{ "receipt": "receipt-123" }` |
| `Development` | `READY_FOR_REVIEW` (2) | `{ "specification": "Build the import workflow" }` |
| `Development` | `DONE` (3) | `{ "branchName": "feature/import-workflow" }` |
| `Development` | `RELEASED` (4) | `{ "versionNumber": "1.2.0" }` |

For unknown task types, the board shows `fallbackJson` and submits the parsed object. Invalid JSON marks the field with `invalidJson` and prevents submission.

## Task state behavior

- `setCurrentUserId(userId)` clears the current error and refreshes `/api/tasks/user/{userId}`.
- `changeTaskStatus(taskId, request)` posts to `/api/tasks/{taskId}/change-status` and syncs the returned task into local state.
- `closeTask(taskId, request)` posts to `/api/tasks/{taskId}/close`.
- `syncTaskWithState()` removes a task when there is no selected user, the task belongs to another user, or `currentStatus === TASK_STATUS.CLOSED`.
- Error messages prefer the backend `{ "error": "..." }` response shape, then string payloads, then the HTTP error message.

## Extending a workflow

When adding a task type or status field:

1. Add or reuse status constants in `TASK_STATUS`; update `DEFAULT_STATUS_LABELS` if users should see a new label.
2. Add the task type final status to `TASK_FINAL_STATUS_BY_TYPE` when the frontend should offer forward movement.
3. Add parent-owned controls to `TaskWorkflowBoardComponent.form`.
4. Hydrate existing task data in `hydrateStatusFields()`.
5. Build the API payload in `buildPayload()`.
6. Add a task-type field component or extend the template switch.
7. Keep backend handler validation as the source of truth and mirror only user-facing client validators in Angular.

Common pitfalls:

- Do not reintroduce numeric status literals in components; import `TASK_STATUS`.
- Do not create controls inside child field components.
- Do not rely on client validators for security or data integrity; backend handlers validate every status change.
- Unknown task types only get fallback JSON editing and cannot infer a final status unless `TASK_FINAL_STATUS_BY_TYPE` is updated.

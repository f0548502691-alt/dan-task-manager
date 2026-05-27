# Angular Frontend Guide

This frontend is a standalone Angular application for the task workflow board. It
loads task type schemas from the backend, renders status-specific fields, and
submits workflow actions through the `/api` proxy.

## Run and build

From `frontend/`:

```bash
npm start
npm run build
npm test
```

- `npm start` runs `ng serve --proxy-config proxy.conf.json`.
- `npm run build` runs the production Angular build.
- `npm test` is currently a placeholder script and does not execute a test
  runner.
- `angular.json` includes only `src/styles.css` as a global stylesheet and uses
  an empty `polyfills` array.

## Application shape

- `src/main.ts` bootstraps `AppComponent` with
  `provideHttpClient(withInterceptors([httpErrorInterceptor]))`,
  `AppErrorHandler`, and `provideZonelessChangeDetection()`.
- `src/app/app.component.ts` hosts the task workflow board.
- `src/app/tasks/task-workflow-board.component.ts` owns the create, status
  update, and close forms.
- `src/app/tasks/task.service.ts` calls `/api/tasks`, `/api/tasks/{id}`,
  `/api/tasks/{id}/change-status`, `/api/tasks/{id}/close`, and
  `/api/task-types`.
- `src/app/tasks/dynamic-task-fields.component.ts` renders schema-driven fields
  inside the status update form.

The board uses status `1` for created tasks and status `99` for closed tasks.
Task lists are normalized from either a paged backend response (`items`) or a
legacy array response, then sorted newest-first by `createdAt`.

## Dynamic workflow fields

`TaskWorkflowBoardComponent` loads active task type metadata through
`TaskService.getTaskTypes()`. When a task is selected, it fetches detail data via
`GET /api/tasks/{id}` so the status form can hydrate `customFields`.

For schemas with field rules, `<app-dynamic-task-fields>` rebuilds the
`customFields` `FormGroup` for the selected status and emits the resolved rules
back to the board. The board then builds the `customFields` payload from the
active controls before calling `POST /api/tasks/{id}/change-status`.

If a task type has no schema fields, the board falls back to the `fallbackJson`
textarea and accepts an object-shaped JSON payload. Empty JSON becomes `{}`.

## Styling and Angular CSS scoping

Angular component styles are scoped to their component view. A selector in
`task-workflow-board.component.css` does not style DOM emitted by
`DynamicTaskFieldsComponent`, even though the dynamic fields are nested in the
board template.

Use these locations for styles:

| Style target | File |
| --- | --- |
| Page and board layout (`.workflow-layout`, panels, task list items, feedback) | `src/app/tasks/task-workflow-board.component.css` |
| Dynamic field layout (`.dynamic-fields`, `.dynamic-fields__checkbox-row`) | `src/app/tasks/dynamic-task-fields.component.css` |
| Shared form element defaults (`label`, `input`, `textarea`, `select`, `button`, `small`) | `src/styles.css` |

Example component-scoped styling:

```ts
@Component({
  selector: 'app-dynamic-task-fields',
  standalone: true,
  templateUrl: './dynamic-task-fields.component.html',
  styleUrl: './dynamic-task-fields.component.css'
})
export class DynamicTaskFieldsComponent {}
```

Keep component-specific selectors with the component that renders the matching
HTML. Promote a selector to `src/styles.css` only when it is intentionally shared
across components, such as base form control styling.

## Common pitfalls

- Do not place `.dynamic-fields` rules in
  `task-workflow-board.component.css`; Angular view encapsulation prevents those
  rules from reaching `DynamicTaskFieldsComponent`.
- When adding a new standalone component, wire its stylesheet through
  `styleUrl` or `styleUrls` in the component decorator.
- Global selectors in `src/styles.css` apply across the whole app. Keep them
  broad and low-specificity.
- Zoneless change detection is enabled. Use signals, reactive forms, or explicit
  state updates for async UI changes.
- The frontend sends public `customFields` payloads. Do not reintroduce legacy
  request keys such as `customDataJson` or `newDataJson`.

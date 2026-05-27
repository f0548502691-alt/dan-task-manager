# Dan Task Manager backend docs - start here

The backend docs include some older generated reports. For current workflow and
API behavior, start with the references below.

## Recommended reading path

1. [README.md](README.md) - setup, source map, seed data, and API highlights.
2. [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md) - task workflow rules,
   supported statuses, and endpoint examples.
3. [API_ERROR_CODES.md](API_ERROR_CODES.md) - validation failures and response
   shapes.
4. [STRATEGY_PATTERN_DOCS.md](STRATEGY_PATTERN_DOCS.md) - metadata rule
   provider precedence and handler fallback behavior.
5. [EXTENSION_GUIDE.md](EXTENSION_GUIDE.md) - checklists for task type and
   feature changes.

## Current contract snapshot

```text
Supported task types: Procurement, Development
Created status:       1
Closed status:        99
Procurement final:    3
Development final:    4
Status change body:   { newStatus, nextAssignedToUserId, customFields }
List response shape:  PagedResult<TaskSummaryDto>
Detail response shape: TaskDetailsDto with customFields
```

## Endpoint map

```text
GET    /api/tasks
GET    /api/tasks/{id}
GET    /api/tasks/byType/{taskType}
GET    /api/tasks/user/{userId}
POST   /api/tasks
POST   /api/tasks/{id}/change-status
POST   /api/tasks/{id}/close
PUT    /api/tasks/{id}
DELETE /api/tasks/{id}

GET    /api/task-types
GET    /api/task-types/{taskType}
GET    /api/task-types/{taskType}/schema
POST   /api/task-types
POST   /api/task-types/{taskType}/fields
PUT    /api/task-types/{taskType}/fields/{field}
```

## Pitfalls to avoid

- Do not use workflow examples that start at status `0`; new tasks start at
  status `1`.
- Do not send `newDataJson` or `customDataJson` in public API requests; use
  `customFields`.
- Do not assume metadata endpoints can create arbitrary task types. Writes are
  restricted to `WorkflowConstants.SupportedTaskTypes`.
- Do not expect `customFields` in list responses. Call `GET /api/tasks/{id}` for
  detail data before editing a workflow form.

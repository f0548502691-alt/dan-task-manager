# Workflow Engine

`TaskWorkflowService` enforces the rules that apply to **every** task type. Per-type
rules are delegated to `ITaskWorkflowRuleProvider` implementations, so adding a new
task type never requires touching the workflow engine.

## General rules (apply to every task)

These are enforced in `TaskWorkflowService.ValidateStatusMovement` and the surrounding
checks inside `ChangeStatusAsync` / `CloseTaskAsync`.

| Rule | Where |
|------|-------|
| A closed task (`CurrentStatus == 99`) is immutable. | `ChangeStatusAsync`, `EnsureTaskMutableAsync` |
| `newStatus == 99` is only reachable via `CloseTaskAsync`. | `ValidateStatusMovement` |
| Forward movement must be exactly `+1` status. | `ValidateStatusMovement` |
| Backward movement may jump to any lower status `>= 1`. | `ValidateStatusMovement` |
| The same status is not a valid transition. | `ValidateStatusMovement` |
| A task already at its `FinalStatus` cannot advance further. | `ValidateStatusMovement` |
| The `nextAssignedToUserId` must reference an existing user. | `ChangeStatusAsync`, `CloseTaskAsync` |
| `CustomDataJson` must parse to a JSON object. | `ChangeStatusAsync` (`IsValidJsonPayload`) |
| `CloseTaskAsync` only succeeds from the task type's `FinalStatus`. | `CloseTaskAsync` |
| `CloseTaskAsync` must also satisfy type-specific data requirements. | `CloseTaskAsync` + `ITaskWorkflowRuleProvider.ValidateClose` |

Status constants live in `Domain/WorkflowConstants.cs`:

- `CreatedStatus = 1`
- `ClosedStatus = 99`

## Per-type rules (rule providers)

The workflow engine asks each registered `ITaskWorkflowRuleProvider` whether it
`CanHandle(taskType)`. Providers are ordered by `Priority` (lowest first); the first
match wins. Two providers ship today:

| Provider | Priority | Source of rules |
|----------|----------|-----------------|
| `MetadataTaskWorkflowRuleProvider` | `0` | Database rows in `TaskTypeMetadata` + `TaskFieldDefinition`. No code required to add a type. |
| `HandlerTaskWorkflowRuleProvider` | `100` | DI-discovered classes implementing `IRegisterableTaskHandler`. Use for rules too complex for declarative metadata. |

Each provider answers four questions per task type:

- `int? GetFinalStatus(string taskType)`
- `ValidationResult ValidateStatusChange(BaseTask task, int nextStatus, string newDataJson)`
- `ValidationResult ValidateClose(BaseTask task, string finalNotes, string closeDataJson)`
- `string BuildCloseData(BaseTask task, string finalNotes)`
- `IReadOnlyCollection<string> GetKnownTaskTypes()` &nbsp;_(used by the startup conflict validator)_

## JSON-field indexing

Fields in `TaskFieldDefinition` carry an `IsIndexed` flag. On SQL Server the
`JsonIndexBootstrapper` startup service materializes each indexed scalar field
as a `JSON_VALUE`-backed computed column (`cd_<fieldKey>`) on the `Tasks`
table and creates a composite index `(TaskType, cd_<fieldKey>)`. This turns
`WHERE TaskType = 'Development' AND JSON_VALUE(CustomDataJson, '$.branchName') = '...'`
queries from full table scans into seekable index lookups. The bootstrap is
idempotent and is skipped silently on the InMemory provider used by tests.

## Startup conflict detection

`TaskTypeConflictValidator` runs once on application startup (registered as an
`IHostedService`) and checks whether any task-type code is claimed by more than one
provider. By default it logs a warning naming the providers involved and the winning
(lowest-priority) one. To make the application refuse to start on conflict, set:

```json
"TaskTypeConflictValidation": { "FailOnConflict": true }
```

This prevents the silent-shadowing scenario where, for example, a `Marketing` row in
metadata coexists with a `MarketingTaskHandler` class — the metadata would win at
runtime and the handler would never execute.

## Procurement task (example)

- `FinalStatus = 3`
- Status `2` requires `prices`: array of exactly 2 non-empty strings.
- Status `3` requires `receipt`: non-empty string.

Defined as metadata in `Data/ApplicationDbContext.SeedData` (rows 1-2 of
`TaskFieldDefinition`).

## Development task (example)

- `FinalStatus = 4`
- Status `2` requires `specification`: string, min length 10.
- Status `3` requires `branchName`: matches the `valid_git_branch` named pattern.
- Status `4` requires `versionNumber`: string-or-number matching `semantic_version`.

Defined as metadata in `Data/ApplicationDbContext.SeedData` (rows 3-5 of
`TaskFieldDefinition`).

## Marketing task (example of a "third type", metadata-only)

- `FinalStatus = 3`
- Status `2` requires `campaignName`: string with min length 3.
- Status `2` requires `targetAudience`: one of `B2B`, `B2C`, `Internal`.
- Status `3` requires `launchDate`: string matching `^\d{4}-\d{2}-\d{2}$`.

Added without touching any C# file: rows 6-8 of `TaskFieldDefinition` and row 3 of
`TaskTypeMetadata` in the seed. This is the canonical demonstration that the
architecture supports new task types without structural changes.

## Adding a new task type

See `docs/EXTENSION_GUIDE.md` for the two paths: metadata-only (preferred) and
code-backed handler (when rules are not expressible declaratively).

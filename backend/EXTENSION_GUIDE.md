# Extension Guide

Use this guide when adding task types, endpoints, request validators, or new
workflow rules. Keep cross-task workflow invariants in the workflow service and
task-specific payload rules in handlers.

## Where extensions belong

| Change | Primary location | Notes |
|--------|------------------|-------|
| New task type | `Domain/Handlers/*TaskHandler.cs` | Implement `ITaskHandler`; registration is automatic |
| New task query or mutation endpoint | `Controllers/*Controller.cs` + application service | Controllers validate/bind; services own data access |
| Request shape validation | `Validation/*RequestValidators.cs` | FluentValidation validators are auto-registered |
| Cross-task workflow invariant | `Services/TaskWorkflowService.cs` | Applies to every task type |
| Task-type payload rule | Handler for that task type | Applies only when the handler receives the target status |
| New response DTO/query model | `Services/QueryModels.cs` | Keep API responses stable and explicit |

## Adding a task handler

Handlers implement the Strategy pattern. `Program.cs` calls
`AddTaskHandlersFromAssembly(typeof(ITaskHandler).Assembly)`, which scans
`DanTaskManager.Domain.Handlers` for concrete `ITaskHandler` implementations and
registers them as transient services.

### Constraints

- Put handlers in namespace `DanTaskManager.Domain.Handlers`.
- Use a unique `TaskType`; `TaskHandlerFactory` stores them case-insensitively.
- Set `FinalStatus` to the last workflow status before close.
- Do not use status `99`; it is reserved for `WorkflowConstants.ClosedStatus`.
- Validate only the payload for the requested target status.

### Example

```csharp
using System.Text.Json;

namespace DanTaskManager.Domain.Handlers;

public class QATaskHandler : StatusValidationTaskHandlerBase
{
    public QATaskHandler()
        : base(new Dictionary<int, Func<string, ValidationResult>>
        {
            [2] = ValidateStatusTwo,
            [3] = ValidateStatusThree
        })
    {
    }

    public override string TaskType => "QA";

    public override int FinalStatus => 3;

    private static ValidationResult ValidateStatusTwo(string newDataJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(newDataJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("testEnvironment", out var environment) ||
                environment.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(environment.GetString()))
            {
                return ValidationResult.Failure("'testEnvironment' is required");
            }

            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"Invalid JSON: {ex.Message}");
        }
    }

    private static ValidationResult ValidateStatusThree(string newDataJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(newDataJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("testResults", out var results) ||
                results.ValueKind != JsonValueKind.String)
            {
                return ValidationResult.Failure("'testResults' is required");
            }

            var value = results.GetString();
            if (value is not ("PASSED" or "FAILED" or "PARTIAL"))
            {
                return ValidationResult.Failure(
                    "'testResults' must be PASSED, FAILED, or PARTIAL");
            }

            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"Invalid JSON: {ex.Message}");
        }
    }
}
```

No manual `Program.cs` registration is needed when the handler satisfies the
namespace/interface constraints.

### Handler tests

Test handler payload validation directly and add workflow tests for final-status
and movement behavior:

```csharp
[Fact]
public void ValidateStatusTwo_WithEnvironment_ShouldPass()
{
    var handler = new QATaskHandler();
    var result = handler.ValidateStatusChange(
        "{}",
        WorkflowConstants.CreatedStatus,
        2,
        "{\"testEnvironment\":\"staging\"}");

    Assert.True(result.IsValid);
}

[Fact]
public async Task ChangeStatus_QATaskBeyondFinalStatus_ShouldFail()
{
    // Arrange a QA task at CurrentStatus = 3 and call ChangeStatusAsync(..., 4, ...)
    // Assert result.Success is false and mentions the final status.
}
```

## Adding or changing workflow rules

Use `TaskWorkflowService` for rules that apply to every task type:

- closed tasks cannot change;
- forward movement is exactly `+1`;
- rollback can move to any lower valid status;
- the next assignee must exist;
- `newDataJson` must be valid JSON;
- `CloseTaskAsync` requires the handler final status.

When adding a workflow rule:

1. Add the rule to `TaskWorkflowService`.
2. Return a `WorkflowResult.FailureResult(...)` with a message callers can show.
3. Add or update `Tests/WorkflowServiceTests.cs`.
4. Update `WORKFLOW_SERVICE_DOCS.md` and `API_ERROR_CODES.md` if the caller-visible
   behavior changes.

Use `WorkflowConstants.CreatedStatus` and `WorkflowConstants.ClosedStatus` in new
code instead of literal `1` and `99`.

## Adding request validation

Request-shape validation belongs in `Validation/` and uses FluentValidation.
`Program.cs` registers validators with:

```csharp
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
```

### Example validator

```csharp
using DanTaskManager.Controllers;
using FluentValidation;

namespace DanTaskManager.Validation;

public class AssignReviewerRequestValidator : AbstractValidator<AssignReviewerRequest>
{
    public AssignReviewerRequestValidator()
    {
        RuleFor(x => x.ReviewerUserId)
            .GreaterThan(0)
            .WithMessage("ReviewerUserId חייב להיות גדול מ-0");

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .WithMessage("Notes לא יכול להיות ארוך מ-1000 תווים");
    }
}
```

Controller pattern:

```csharp
var validation = await _assignReviewerValidator.ValidateAsync(
    request,
    HttpContext.RequestAborted);

if (!validation.IsValid)
{
    return BadRequest(new
    {
        error = string.Join("; ", validation.Errors
            .Select(e => e.ErrorMessage)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct())
    });
}
```

Keep validators focused on request shape and syntax. Checks that require database
state, such as "does the assignee exist?", belong in application/workflow services.

## Adding an endpoint

Follow the current layering:

1. Add a request DTO near the controller when the DTO is endpoint-specific.
2. Add a FluentValidation validator when the request has required fields or syntax
   constraints.
3. Add an application service method to own business/data access.
4. Keep controller behavior limited to binding, validation, status-code mapping,
   and logging.
5. Return explicit DTOs from `Services/QueryModels.cs` for reusable response shapes.

### Example outline

```csharp
[HttpPost("{id}/reviewer")]
public async Task<IActionResult> AssignReviewer(
    int id,
    AssignReviewerRequest request)
{
    var validation = await _assignReviewerValidator.ValidateAsync(
        request,
        HttpContext.RequestAborted);

    if (!validation.IsValid)
    {
        return BadRequest(new { error = BuildValidationErrorMessage(validation.Errors.Select(e => e.ErrorMessage)) });
    }

    var result = await _taskService.AssignReviewerAsync(
        id,
        request.ReviewerUserId,
        HttpContext.RequestAborted);

    if (!result.Success)
    {
        throw new WorkflowValidationException(result.Message);
    }

    return NoContent();
}
```

If the operation changes a closed task, call `EnsureTaskMutableAsync` through the
application service before mutating data.

## Pagination and query responses

Use `PageRequest` and `PagedResult<T>` for list endpoints. Current behavior:

- `page < 1` becomes `1`;
- `pageSize < 1` becomes `20`;
- `pageSize > 100` becomes `100`;
- task lists are ordered by newest `CreatedAt` first;
- task list items are `TaskSummaryDto` and do not include `customDataJson`.

Use `TaskDetailsDto` for detail responses that need the JSON payload.

## Testing checklist

For handler changes:

- direct handler tests for every required target status;
- invalid JSON and missing-field cases;
- workflow tests for final-status enforcement.

For workflow changes:

- forward movement, rollback, same-status rejection, and close path;
- missing task and missing next assignee;
- closed-task immutability for update/delete/status changes;
- response message expectations when callers depend on them.

For API/request validation changes:

- validator unit tests or controller tests for invalid request shape;
- confirm error aggregation when multiple fields fail;
- update docs for any new public request/response fields.

## Common pitfalls

- Manual handler registration in `Program.cs` is stale; use the assembly scanner.
- Do not add database checks to FluentValidation validators unless the project
  explicitly introduces async validators and tests their DI behavior.
- Do not return EF entities from new endpoints; prefer DTOs from `QueryModels.cs`.
- Do not expect `TaskSummaryDto` to carry `customDataJson`.
- Remember that rollback replaces `CustomDataJson` with the submitted JSON.
- Closing malformed existing JSON preserves closure by replacing the payload with
  `finalNotes` and `closedAt`.

## Related documentation

- [README.md](README.md) - backend architecture and setup
- [WORKFLOW_SERVICE_DOCS.md](WORKFLOW_SERVICE_DOCS.md) - workflow service behavior
- [API_ERROR_CODES.md](API_ERROR_CODES.md) - public API error shapes
- [STRATEGY_PATTERN_DOCS.md](STRATEGY_PATTERN_DOCS.md) - handler design background

using DanTaskManager.Controllers;
using FluentValidation;
using System.Text.Json;

namespace DanTaskManager.Validation;

public class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.TaskType)
            .NotEmpty()
            .WithMessage("TaskType is required");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description is required");

        RuleFor(x => x.AssignedToUserId)
            .GreaterThan(0)
            .WithMessage("AssignedToUserId must be greater than 0");

        RuleFor(x => x.CustomFields)
            .Must(BeValidCustomFields)
            .WithMessage("CustomFields must be a valid JSON object");
    }

    private static bool BeValidCustomFields(JsonElement? value)
    {
        if (!value.HasValue)
        {
            return true;
        }

        return value.Value.ValueKind == JsonValueKind.Object;
    }
}

public class ChangeStatusWorkflowRequestValidator : AbstractValidator<ChangeStatusWorkflowRequest>
{
    public ChangeStatusWorkflowRequestValidator()
    {
        RuleFor(x => x.NewStatus)
            .GreaterThan(0)
            .WithMessage("NewStatus must be greater than 0");

        RuleFor(x => x.NextAssignedToUserId)
            .GreaterThan(0)
            .WithMessage("NextAssignedToUserId is required");

        RuleFor(x => x.CustomFields)
            .NotNull()
            .WithMessage("CustomFields is required")
            .Must(BeValidCustomFields)
            .WithMessage("CustomFields must be a valid JSON object");
    }

    private static bool BeValidCustomFields(JsonElement? value)
    {
        if (!value.HasValue)
        {
            return false;
        }

        return value.Value.ValueKind == JsonValueKind.Object;
    }
}

public class CloseTaskRequestValidator : AbstractValidator<CloseTaskRequest>
{
    public CloseTaskRequestValidator()
    {
        RuleFor(x => x.NextAssignedToUserId)
            .GreaterThan(0)
            .WithMessage("NextAssignedToUserId נדרש");

        RuleFor(x => x.FinalNotes)
            .NotEmpty()
            .WithMessage("FinalNotes is required");
    }
}

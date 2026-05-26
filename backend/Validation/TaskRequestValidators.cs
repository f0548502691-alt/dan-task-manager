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
            .WithMessage("TaskType נדרש");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description נדרש");

        RuleFor(x => x.AssignedToUserId)
            .GreaterThan(0)
            .WithMessage("AssignedToUserId חייב להיות גדול מ-0");

        RuleFor(x => x.CustomDataJson)
            .Must(BeValidJson)
            .When(x => !string.IsNullOrWhiteSpace(x.CustomDataJson))
            .WithMessage("CustomDataJson חייב להיות JSON תקין");
    }

    private static bool BeValidJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public class ChangeStatusWorkflowRequestValidator : AbstractValidator<ChangeStatusWorkflowRequest>
{
    public ChangeStatusWorkflowRequestValidator()
    {
        RuleFor(x => x.NewStatus)
            .GreaterThan(0)
            .WithMessage("NewStatus חייב להיות גדול מ-0");

        RuleFor(x => x.NextAssignedToUserId)
            .GreaterThan(0)
            .WithMessage("NextAssignedToUserId נדרש");

        RuleFor(x => x.NewDataJson)
            .NotEmpty()
            .WithMessage("NewDataJson נדרש")
            .Must(BeValidJson)
            .WithMessage("NewDataJson חייב להיות JSON תקין");
    }

    private static bool BeValidJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public class CloseTaskRequestValidator : AbstractValidator<CloseTaskRequest>
{
    public CloseTaskRequestValidator()
    {
        RuleFor(x => x.FinalNotes)
            .NotEmpty()
            .WithMessage("FinalNotes נדרש");
    }
}

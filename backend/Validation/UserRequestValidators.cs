using DanTaskManager.Controllers;
using FluentValidation;

namespace DanTaskManager.Validation;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name נדרש")
            .MaximumLength(255)
            .WithMessage("Name לא יכול להיות ארוך מ-255 תווים");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email נדרש")
            .EmailAddress()
            .WithMessage("Email לא תקין")
            .MaximumLength(255)
            .WithMessage("Email לא יכול להיות ארוך מ-255 תווים");
    }
}

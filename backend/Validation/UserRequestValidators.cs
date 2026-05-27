using DanTaskManager.Controllers;
using FluentValidation;

namespace DanTaskManager.Validation;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MaximumLength(255)
            .WithMessage("Name cannot exceed 255 characters");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Email format is invalid")
            .MaximumLength(255)
            .WithMessage("Email cannot exceed 255 characters");
    }
}

using DanTaskManager.Controllers;
using DanTaskManager.Validation;

namespace DanTaskManager.Tests;

public class RequestValidatorTests
{
    [Fact]
    public void CreateTaskRequestValidator_WithInvalidJson_ShouldRejectRequest()
    {
        var validator = new CreateTaskRequestValidator();

        var result = validator.Validate(new CreateTaskRequest
        {
            TaskType = "Procurement",
            Description = "Buy equipment",
            AssignedToUserId = 1,
            CustomDataJson = "{not-json"
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTaskRequest.CustomDataJson));
    }

    [Fact]
    public void ChangeStatusWorkflowRequestValidator_WithMissingAssigneeAndInvalidJson_ShouldRejectRequest()
    {
        var validator = new ChangeStatusWorkflowRequestValidator();

        var result = validator.Validate(new ChangeStatusWorkflowRequest
        {
            NewStatus = 2,
            NextAssignedToUserId = 0,
            NewDataJson = ""
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ChangeStatusWorkflowRequest.NextAssignedToUserId));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ChangeStatusWorkflowRequest.NewDataJson));
    }

    [Fact]
    public void CloseTaskRequestValidator_WithBlankFinalNotes_ShouldRejectRequest()
    {
        var validator = new CloseTaskRequestValidator();

        var result = validator.Validate(new CloseTaskRequest { FinalNotes = "   " });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CloseTaskRequest.FinalNotes));
    }

    [Fact]
    public void CreateUserRequestValidator_WithInvalidEmail_ShouldRejectRequest()
    {
        var validator = new CreateUserRequestValidator();

        var result = validator.Validate(new CreateUserRequest
        {
            Name = "Dana",
            Email = "not-an-email"
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUserRequest.Email));
    }
}

using DanTaskManager.Controllers;
using DanTaskManager.Validation;
using System.Text.Json;

namespace DanTaskManager.Tests;

public class TaskRequestValidatorTests
{
    [Fact]
    public void ChangeStatusWorkflowRequestValidator_WithMissingCustomFields_Fails()
    {
        var validator = new ChangeStatusWorkflowRequestValidator();
        var request = new ChangeStatusWorkflowRequest
        {
            NewStatus = 2,
            NextAssignedToUserId = 1,
            CustomFields = null
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("CustomFields נדרש"));
    }

    [Fact]
    public void ChangeStatusWorkflowRequestValidator_WithArrayCustomFields_Fails()
    {
        var validator = new ChangeStatusWorkflowRequestValidator();
        using var document = JsonDocument.Parse("[]");
        var request = new ChangeStatusWorkflowRequest
        {
            NewStatus = 2,
            NextAssignedToUserId = 1,
            CustomFields = document.RootElement.Clone()
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("CustomFields חייב להיות אובייקט JSON"));
    }

    [Fact]
    public void CloseTaskRequestValidator_WithMissingNextAssignee_Fails()
    {
        var validator = new CloseTaskRequestValidator();
        var request = new CloseTaskRequest
        {
            NextAssignedToUserId = 0,
            FinalNotes = "Done"
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("NextAssignedToUserId נדרש"));
    }
}

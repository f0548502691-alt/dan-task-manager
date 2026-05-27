using DanTaskManager.Contracts.Requests.Tasks;
using DanTaskManager.Validation;
using System.Text.Json;

namespace DanTaskManager.Tests;

public class TaskRequestValidatorTests
{
    [Fact]
    public async Task CreateTaskRequestValidator_WhenCustomFieldsIsArray_RejectsPayload()
    {
        using var customFields = JsonDocument.Parse("[\"not\", \"an\", \"object\"]");
        var request = new CreateTaskRequest
        {
            TaskType = "Development",
            Description = "Implement error handling",
            AssignedToUserId = 7,
            CustomFields = customFields.RootElement
        };

        var result = await new CreateTaskRequestValidator().ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(CreateTaskRequest.CustomFields) &&
            error.ErrorMessage.Contains("CustomFields"));
    }

    [Fact]
    public async Task ChangeStatusWorkflowRequestValidator_WhenCustomFieldsIsMissing_RejectsPayload()
    {
        var request = new ChangeStatusWorkflowRequest
        {
            NewStatus = 2,
            NextAssignedToUserId = 9
        };

        var result = await new ChangeStatusWorkflowRequestValidator().ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(ChangeStatusWorkflowRequest.CustomFields));
    }

    [Fact]
    public async Task ChangeStatusWorkflowRequestValidator_WhenCustomFieldsIsObject_AcceptsPayload()
    {
        using var customFields = JsonDocument.Parse("{\"branchName\":\"feature/error-contract\"}");
        var request = new ChangeStatusWorkflowRequest
        {
            NewStatus = 3,
            NextAssignedToUserId = 9,
            CustomFields = customFields.RootElement
        };

        var result = await new ChangeStatusWorkflowRequestValidator().ValidateAsync(request);

        Assert.True(result.IsValid);
    }
}

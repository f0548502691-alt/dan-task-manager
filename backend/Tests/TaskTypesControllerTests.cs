using DanTaskManager.Controllers;
using DanTaskManager.Domain;
using DanTaskManager.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DanTaskManager.Tests;

public class TaskTypesControllerTests
{
    [Fact]
    public void UpsertTaskType_WithUnsupportedType_ReturnsBadRequestWithSupportedTypes()
    {
        var metadataService = new RecordingTaskTypeMetadataService();
        var controller = new TaskTypesController(metadataService);

        var result = controller.UpsertTaskType(new UpsertTaskTypeRequest(
            "Analysis",
            "Analysis",
            2));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var responseJson = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("Analysis", responseJson);
        Assert.Contains("Development", responseJson);
        Assert.Contains("Procurement", responseJson);
        Assert.False(metadataService.UpsertTaskTypeCalled);
    }

    [Fact]
    public void UpsertTaskTypeField_WithUnsupportedType_ReturnsBadRequestWithoutMutatingMetadata()
    {
        var metadataService = new RecordingTaskTypeMetadataService();
        var controller = new TaskTypesController(metadataService);

        var result = controller.UpsertTaskTypeField(
            "Testing",
            new UpsertTaskTypeFieldRequest(
                Field: "coverage",
                Type: "string",
                Required: true,
                AppliesFromStatus: 3,
                AppliesToStatus: 3));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var responseJson = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("Testing", responseJson);
        Assert.Contains("Development", responseJson);
        Assert.Contains("Procurement", responseJson);
        Assert.False(metadataService.UpsertFieldDefinitionCalled);
    }

    [Fact]
    public void UpsertTaskType_WithSupportedType_DelegatesToMetadataService()
    {
        var metadataService = new RecordingTaskTypeMetadataService();
        var controller = new TaskTypesController(metadataService);

        var result = controller.UpsertTaskType(new UpsertTaskTypeRequest(
            "Procurement",
            "Procurement",
            3));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var schema = Assert.IsType<TaskTypeSchemaDto>(ok.Value);
        Assert.Equal("Procurement", schema.TaskType);
        Assert.True(metadataService.UpsertTaskTypeCalled);
    }

    private class RecordingTaskTypeMetadataService : ITaskTypeMetadataService
    {
        public bool UpsertTaskTypeCalled { get; private set; }
        public bool UpsertFieldDefinitionCalled { get; private set; }

        public IReadOnlyCollection<TaskTypeSchemaDto> GetTaskTypes()
        {
            return WorkflowConstants.SupportedTaskTypes
                .Select(type => new TaskTypeSchemaDto
                {
                    TaskType = type,
                    DisplayName = type,
                    FinalStatus = type == "Procurement" ? 3 : 4,
                    IsActive = true,
                    Version = 1
                })
                .ToArray();
        }

        public TaskTypeSchemaDto? GetTaskType(string taskType)
        {
            return GetTaskTypes()
                .FirstOrDefault(schema => schema.TaskType.Equals(taskType, StringComparison.OrdinalIgnoreCase));
        }

        public MetadataOperationResult UpsertTaskType(UpsertTaskTypeCommand command)
        {
            UpsertTaskTypeCalled = true;
            var schema = new TaskTypeSchemaDto
            {
                TaskType = command.TaskType,
                DisplayName = command.DisplayName ?? command.TaskType,
                FinalStatus = command.FinalStatus,
                IsActive = command.IsActive,
                Version = 1
            };
            return MetadataOperationResult.SuccessResult(schema);
        }

        public MetadataOperationResult UpsertFieldDefinition(string taskType, UpsertFieldDefinitionCommand command)
        {
            UpsertFieldDefinitionCalled = true;
            var schema = new TaskTypeSchemaDto
            {
                TaskType = taskType,
                DisplayName = taskType,
                FinalStatus = taskType.Equals("Procurement", StringComparison.OrdinalIgnoreCase) ? 3 : 4,
                IsActive = true,
                Version = 1,
                Fields = new[] { new FieldRuleDefinition { Field = command.Field, Type = command.Type } }
            };
            return MetadataOperationResult.SuccessResult(schema);
        }
    }
}

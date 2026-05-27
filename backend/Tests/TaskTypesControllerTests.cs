using DanTaskManager.Controllers;
using DanTaskManager.Domain.Handlers;
using DanTaskManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace DanTaskManager.Tests;

public class TaskTypesControllerTests
{
    [Fact]
    public void GetTaskTypes_IncludesHandlerBackedTypesMissingFromMetadata()
    {
        var controller = CreateController([
            new TaskTypeSchemaDto
            {
                TaskType = "Development",
                DisplayName = "Development Metadata",
                FinalStatus = 4,
                IsActive = true,
                Version = 2,
                Fields =
                [
                    new FieldRuleDefinition
                    {
                        Field = "branchName",
                        Type = "string",
                        Required = true,
                        AppliesFromStatus = 3,
                        AppliesToStatus = 3
                    }
                ]
            },
            new TaskTypeSchemaDto
            {
                TaskType = "Retired",
                DisplayName = "Retired",
                FinalStatus = 2,
                IsActive = false,
                Version = 1
            }
        ]);

        var schemas = GetOkValue<IReadOnlyCollection<TaskTypeSchemaDto>>(controller.GetTaskTypes());

        Assert.Equal(
            new[] { "Analysis", "Development", "Testing" },
            schemas.Select(schema => schema.TaskType));

        var handlerBacked = Assert.Single(schemas, schema => schema.TaskType == "Analysis");
        Assert.Equal("Analysis", handlerBacked.DisplayName);
        Assert.Equal(2, handlerBacked.FinalStatus);
        Assert.True(handlerBacked.IsActive);
        Assert.Empty(handlerBacked.Fields);

        var metadataBacked = Assert.Single(schemas, schema => schema.TaskType == "Development");
        Assert.Equal("Development Metadata", metadataBacked.DisplayName);
        Assert.Single(metadataBacked.Fields);
    }

    [Fact]
    public void GetTaskTypeSchema_WhenMetadataExists_ReturnsMetadataSchema()
    {
        var controller = CreateController([
            new TaskTypeSchemaDto
            {
                TaskType = "Development",
                DisplayName = "Development Metadata",
                FinalStatus = 4,
                IsActive = true,
                Version = 2,
                Fields =
                [
                    new FieldRuleDefinition
                    {
                        Field = "versionNumber",
                        Type = "string",
                        Required = true,
                        AppliesFromStatus = 4,
                        AppliesToStatus = 4
                    }
                ]
            }
        ]);

        var schema = GetOkValue<TaskTypeSchemaDto>(controller.GetTaskTypeSchema("development"));

        Assert.Equal("Development", schema.TaskType);
        Assert.Equal("Development Metadata", schema.DisplayName);
        Assert.Equal(4, schema.FinalStatus);
        Assert.Single(schema.Fields);
    }

    [Fact]
    public void GetTaskTypeSchema_WhenOnlyHandlerExists_ReturnsHandlerBackedSchema()
    {
        var controller = CreateController([]);

        var schema = GetOkValue<TaskTypeSchemaDto>(controller.GetTaskTypeSchema("analysis"));

        Assert.Equal("Analysis", schema.TaskType);
        Assert.Equal("Analysis", schema.DisplayName);
        Assert.Equal(2, schema.FinalStatus);
        Assert.True(schema.IsActive);
        Assert.Equal(1, schema.Version);
        Assert.Empty(schema.Fields);
    }

    [Fact]
    public void GetTaskTypeSchemaAlias_ReturnsSameHandlerBackedSchemaAsPrimaryRoute()
    {
        var controller = CreateController([]);

        var schema = GetOkValue<TaskTypeSchemaDto>(controller.GetTaskTypeSchemaAlias("testing"));

        Assert.Equal("Testing", schema.TaskType);
        Assert.Equal(3, schema.FinalStatus);
        Assert.Empty(schema.Fields);
    }

    [Fact]
    public void GetTaskTypeSchema_WhenTypeIsUnknown_ReturnsNotFound()
    {
        var controller = CreateController([]);

        var result = controller.GetTaskTypeSchema("Unknown");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static TaskTypesController CreateController(IReadOnlyCollection<TaskTypeSchemaDto> schemas)
    {
        var metadataService = new StubTaskTypeMetadataService(schemas);
        var handlerFactory = new TaskHandlerFactory([
            new AnalysisTaskHandler(),
            new TestingTaskHandler()
        ]);
        var catalog = new TaskTypeCatalogService(metadataService, handlerFactory);

        return new TaskTypesController(metadataService, catalog);
    }

    private static T GetOkValue<T>(ActionResult<T> actionResult)
    {
        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    private sealed class StubTaskTypeMetadataService : ITaskTypeMetadataService
    {
        private readonly IReadOnlyCollection<TaskTypeSchemaDto> _schemas;

        public StubTaskTypeMetadataService(IReadOnlyCollection<TaskTypeSchemaDto> schemas)
        {
            _schemas = schemas;
        }

        public IReadOnlyCollection<TaskTypeSchemaDto> GetTaskTypes() => _schemas;

        public TaskTypeSchemaDto? GetTaskType(string taskType)
        {
            return _schemas.FirstOrDefault(schema =>
                schema.TaskType.Equals(taskType, StringComparison.OrdinalIgnoreCase));
        }

        public MetadataOperationResult UpsertTaskType(UpsertTaskTypeCommand command) =>
            throw new NotSupportedException();

        public MetadataOperationResult UpsertFieldDefinition(
            string taskType,
            UpsertFieldDefinitionCommand command) =>
            throw new NotSupportedException();
    }
}

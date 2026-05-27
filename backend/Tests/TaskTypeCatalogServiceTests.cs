using DanTaskManager.Domain.Handlers;
using DanTaskManager.Services;

namespace DanTaskManager.Tests;

public class TaskTypeCatalogServiceTests
{
    [Fact]
    public void GetTaskTypes_MergesActiveMetadataWithRegisteredHandlers()
    {
        var metadata = new StubTaskTypeMetadataService([
            new TaskTypeSchemaDto
            {
                TaskType = "Development",
                DisplayName = "Custom Development",
                FinalStatus = 7,
                IsActive = true,
                Version = 2
            },
            new TaskTypeSchemaDto
            {
                TaskType = "Procurement",
                DisplayName = "Procurement Metadata",
                FinalStatus = null,
                IsActive = true,
                Version = 3
            },
            new TaskTypeSchemaDto
            {
                TaskType = "MetadataOnly",
                DisplayName = "Metadata Only",
                FinalStatus = 5,
                IsActive = true,
                Version = 1
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
        var catalog = new TaskTypeCatalogService(metadata, CreateHandlerFactory());

        var taskTypes = catalog.GetTaskTypes().ToArray();

        Assert.Equal(
            new[] { "Analysis", "Development", "MetadataOnly", "Procurement" },
            taskTypes.Select(type => type.TaskType));

        var analysis = Assert.Single(taskTypes, type => type.TaskType == "Analysis");
        Assert.False(analysis.HasMetadata);
        Assert.True(analysis.HasHandler);
        Assert.Equal(2, analysis.FinalStatus);

        var development = Assert.Single(taskTypes, type => type.TaskType == "Development");
        Assert.Equal("Custom Development", development.DisplayName);
        Assert.True(development.HasMetadata);
        Assert.True(development.HasHandler);
        Assert.Equal(7, development.FinalStatus);

        var procurement = Assert.Single(taskTypes, type => type.TaskType == "Procurement");
        Assert.True(procurement.HasMetadata);
        Assert.True(procurement.HasHandler);
        Assert.Equal(3, procurement.FinalStatus);

        var metadataOnly = Assert.Single(taskTypes, type => type.TaskType == "MetadataOnly");
        Assert.True(metadataOnly.HasMetadata);
        Assert.False(metadataOnly.HasHandler);
        Assert.Equal(5, metadataOnly.FinalStatus);
    }

    [Fact]
    public void Find_TrimsInputAndReturnsCanonicalHandlerType()
    {
        var catalog = new TaskTypeCatalogService(
            new StubTaskTypeMetadataService([]),
            CreateHandlerFactory());

        var descriptor = catalog.Find(" analysis ");

        Assert.NotNull(descriptor);
        Assert.Equal("Analysis", descriptor!.TaskType);
        Assert.False(descriptor.HasMetadata);
        Assert.True(descriptor.HasHandler);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Find_WhenInputIsBlank_ReturnsNull(string? taskType)
    {
        var catalog = new TaskTypeCatalogService(
            new StubTaskTypeMetadataService([]),
            CreateHandlerFactory());

        var descriptor = catalog.Find(taskType);

        Assert.Null(descriptor);
    }

    private static TaskHandlerFactory CreateHandlerFactory()
    {
        return new TaskHandlerFactory([
            new AnalysisTaskHandler(),
            new DevelopmentTaskHandler(),
            new ProcurementTaskHandler()
        ]);
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

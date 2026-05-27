using DanTaskManager.Data;
using DanTaskManager.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using Xunit;

namespace DanTaskManager.Tests;

public class TaskTypeMetadataServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly MemoryCache _cache;
    private readonly TaskTypeValidationService _service;

    public TaskTypeMetadataServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"TaskTypeMetadataServiceTests-{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _cache = new MemoryCache(new MemoryCacheOptions());
        _service = new TaskTypeValidationService(_context, _cache);
    }

    [Fact]
    public void GetTaskType_WithSeededTaskType_ReturnsSchema()
    {
        var schema = _service.GetTaskType("Development");

        Assert.NotNull(schema);
        Assert.Equal("Development", schema!.TaskType);
        Assert.Equal(4, schema.FinalStatus);
        Assert.Contains(schema.Fields, field => field.Field == "branchName");
    }

    [Fact]
    public void UpsertTaskType_CreatesNewTaskType()
    {
        var result = _service.UpsertTaskType(new UpsertTaskTypeCommand(
            TaskType: "QaReview",
            DisplayName: "QA Review",
            FinalStatus: 5,
            IsActive: true));

        Assert.True(result.Success);
        Assert.NotNull(result.TaskType);
        Assert.Equal("QaReview", result.TaskType!.TaskType);
        Assert.Equal(5, result.TaskType.FinalStatus);
    }

    [Fact]
    public void UpsertTaskType_WithoutFinalStatus_Fails()
    {
        var result = _service.UpsertTaskType(new UpsertTaskTypeCommand(
            TaskType: "QaReview",
            DisplayName: "QA Review",
            FinalStatus: null,
            IsActive: true));

        Assert.False(result.Success);
        Assert.Contains("FinalStatus is required", result.Message);
    }

    [Fact]
    public void UpsertTaskType_WithInvalidFinalStatus_Fails()
    {
        var result = _service.UpsertTaskType(new UpsertTaskTypeCommand(
            TaskType: "QaReview",
            DisplayName: "QA Review",
            FinalStatus: 0,
            IsActive: true));

        Assert.False(result.Success);
        Assert.Contains("greater than or equal to 1", result.Message);
    }

    [Fact]
    public void UpsertTaskType_WithClosedStatusAsFinal_Fails()
    {
        var result = _service.UpsertTaskType(new UpsertTaskTypeCommand(
            TaskType: "QaReview",
            DisplayName: "QA Review",
            FinalStatus: 99,
            IsActive: true));

        Assert.False(result.Success);
        Assert.Contains("less than 99", result.Message);
    }

    [Fact]
    public void ValidateStatusData_UsesDbMetadataRules()
    {
        var definitionResult = _service.UpsertFieldDefinition("Development", new UpsertFieldDefinitionCommand
        {
            Field = "releaseType",
            Type = "string",
            Required = true,
            AppliesFromStatus = 4,
            AppliesToStatus = 4,
            AllowedValues = ["major", "minor", "patch"]
        });

        Assert.True(definitionResult.Success);

        var invalidPayload = JsonSerializer.Serialize(new
        {
            versionNumber = "1.0.0",
            releaseType = "hotfix"
        });
        var validPayload = JsonSerializer.Serialize(new
        {
            versionNumber = "1.0.0",
            releaseType = "minor"
        });

        var invalidResult = _service.ValidateStatusData("Development", 4, invalidPayload);
        var validResult = _service.ValidateStatusData("Development", 4, validPayload);

        Assert.False(invalidResult.IsValid);
        Assert.True(validResult.IsValid);
    }

    [Fact]
    public void UpsertFieldDefinition_WhenStatusIsAboveFinalStatus_Fails()
    {
        var result = _service.UpsertFieldDefinition("Development", new UpsertFieldDefinitionCommand
        {
            Field = "postReleaseNote",
            Type = "string",
            Required = true,
            AppliesFromStatus = 5,
            AppliesToStatus = 5
        });

        Assert.False(result.Success);
        Assert.Contains("cannot be greater than FinalStatus", result.Message);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

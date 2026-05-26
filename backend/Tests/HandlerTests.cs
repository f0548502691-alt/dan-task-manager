using DanTaskManager.Domain.Handlers;
using System.Text.Json;

namespace DanTaskManager.Tests;

/// <summary>
/// בדיקות יחידתיות עבור ProcurementTaskHandler
/// </summary>
public class ProcurementTaskHandlerTests
{
    private readonly ProcurementTaskHandler _handler = new();

    // === בדיקות סטטוס 2 ===

    [Fact]
    public void ValidateStatus2_WithValidPrices_ShouldPass()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new { prices = new[] { "5000", "4800" } });

        // Act
        var result = _handler.ValidateStatusChange("{}", 1, 2, json);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Message);
    }

    [Fact]
    public void ValidateStatus2_WithMissingPrices_ShouldFail()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new { });

        // Act
        var result = _handler.ValidateStatusChange("{}", 1, 2, json);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("prices", result.Message);
    }

    [Fact]
    public void ValidateStatus2_WithOnlyOnePrice_ShouldFail()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new { prices = new[] { "5000" } });

        // Act
        var result = _handler.ValidateStatusChange("{}", 1, 2, json);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("2", result.Message);
    }

    [Fact]
    public void ValidateStatus2_WithEmptyPrice_ShouldFail()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new { prices = new[] { "", "4800" } });

        // Act
        var result = _handler.ValidateStatusChange("{}", 1, 2, json);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("ריק", result.Message);
    }

    // === בדיקות סטטוס 3 ===

    [Fact]
    public void ValidateStatus3_WithValidReceipt_ShouldPass()
    {
        // Arrange
        var currentJson = JsonSerializer.Serialize(new { prices = new[] { "5000", "4800" } });
        var newJson = JsonSerializer.Serialize(new { receipt = "REC-2026-001" });

        // Act
        var result = _handler.ValidateStatusChange(currentJson, 2, 3, newJson);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateStatus3_WithMissingReceipt_ShouldFail()
    {
        // Arrange
        var currentJson = JsonSerializer.Serialize(new { prices = new[] { "5000", "4800" } });
        var newJson = JsonSerializer.Serialize(new { });

        // Act
        var result = _handler.ValidateStatusChange(currentJson, 2, 3, newJson);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("receipt", result.Message);
    }

    [Fact]
    public void ValidateStatus3_WithEmptyReceipt_ShouldFail()
    {
        // Arrange
        var currentJson = JsonSerializer.Serialize(new { prices = new[] { "5000", "4800" } });
        var newJson = JsonSerializer.Serialize(new { receipt = "" });

        // Act
        var result = _handler.ValidateStatusChange(currentJson, 2, 3, newJson);

        // Assert
        Assert.False(result.IsValid);
    }

    // === בדיקות סטטוס סופי ===

    [Fact]
    public void ValidateStatus_AtFinalStatus_ShouldFail()
    {
        // Arrange
        var json = "{}";

        // Act
        var result = _handler.ValidateStatusChange(json, 3, 4, json);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Procurement", result.Message);
        Assert.Contains("3", result.Message);
    }

    [Fact]
    public void Handler_FinalStatus_ShouldBe3()
    {
        // Assert
        Assert.Equal(3, _handler.FinalStatus);
    }

    [Fact]
    public void Handler_TaskType_ShouldBeProcurement()
    {
        // Assert
        Assert.Equal("Procurement", _handler.TaskType);
    }
}

/// <summary>
/// בדיקות יחידתיות עבור DevelopmentTaskHandler
/// </summary>
public class DevelopmentTaskHandlerTests
{
    private readonly DevelopmentTaskHandler _handler = new();

    // === בדיקות סטטוס 2 ===

    [Fact]
    public void ValidateStatus2_WithValidSpecification_ShouldPass()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new 
        { 
            specification = "יש לפתח מודול ניהול משתמשים עם Swagger UI ו-JWT authentication"
        });

        // Act
        var result = _handler.ValidateStatusChange("{}", 1, 2, json);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateStatus2_WithTooShortSpecification_ShouldFail()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new { specification = "קצר" });

        // Act
        var result = _handler.ValidateStatusChange("{}", 1, 2, json);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("10", result.Message);
    }

    [Fact]
    public void ValidateStatus2_WithMissingSpecification_ShouldFail()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new { });

        // Act
        var result = _handler.ValidateStatusChange("{}", 1, 2, json);

        // Assert
        Assert.False(result.IsValid);
    }

    // === בדיקות סטטוס 3 ===

    [Fact]
    public void ValidateStatus3_WithValidBranchName_ShouldPass()
    {
        // Arrange
        var currentJson = JsonSerializer.Serialize(new { specification = "תיאור ארוך מספיק..." });
        var newJson = JsonSerializer.Serialize(new { branchName = "feature/user-management" });

        // Act
        var result = _handler.ValidateStatusChange(currentJson, 2, 3, newJson);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateStatus3_WithDoubleSlashInBranchName_ShouldFail()
    {
        // Arrange
        var currentJson = JsonSerializer.Serialize(new { specification = "תיאור..." });
        var newJson = JsonSerializer.Serialize(new { branchName = "feature//user" });

        // Act
        var result = _handler.ValidateStatusChange(currentJson, 2, 3, newJson);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateStatus3_WithSpaceInBranchName_ShouldFail()
    {
        // Arrange
        var currentJson = JsonSerializer.Serialize(new { specification = "תיאור..." });
        var newJson = JsonSerializer.Serialize(new { branchName = "feature user" });

        // Act
        var result = _handler.ValidateStatusChange(currentJson, 2, 3, newJson);

        // Assert
        Assert.False(result.IsValid);
    }

    // === בדיקות סטטוס 4 ===

    [Fact]
    public void ValidateStatus4_WithValidVersionSemVer_ShouldPass()
    {
        // Arrange
        var currentJson = JsonSerializer.Serialize(new 
        { 
            specification = "תיאור...",
            branchName = "feature/user"
        });
        var newJson = JsonSerializer.Serialize(new { versionNumber = "1.2.3" });

        // Act
        var result = _handler.ValidateStatusChange(currentJson, 3, 4, newJson);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateStatus4_WithNumericVersion_ShouldPass()
    {
        // Arrange
        var currentJson = JsonSerializer.Serialize(new { branchName = "feature/user" });
        var newJson = JsonSerializer.Serialize(new { versionNumber = 1.5 });

        // Act
        var result = _handler.ValidateStatusChange(currentJson, 3, 4, newJson);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateStatus4_WithInvalidVersionFormat_ShouldFail()
    {
        // Arrange
        var currentJson = JsonSerializer.Serialize(new { branchName = "feature/user" });
        var newJson = JsonSerializer.Serialize(new { versionNumber = "1.2.a" });

        // Act
        var result = _handler.ValidateStatusChange(currentJson, 3, 4, newJson);

        // Assert
        Assert.False(result.IsValid);
    }

    // === בדיקות סטטוס סופי ===

    [Fact]
    public void Handler_FinalStatus_ShouldBe4()
    {
        // Assert
        Assert.Equal(4, _handler.FinalStatus);
    }

    [Fact]
    public void Handler_TaskType_ShouldBeDevelopment()
    {
        // Assert
        Assert.Equal("Development", _handler.TaskType);
    }
}

/// <summary>
/// בדיקות יחידתיות עבור TaskHandlerFactory
/// </summary>
public class TaskHandlerFactoryTests
{
    [Fact]
    public void GetHandler_WithValidProcurementType_ShouldReturnHandler()
    {
        // Arrange
        var handlers = new ITaskHandler[] 
        { 
            new ProcurementTaskHandler(),
            new DevelopmentTaskHandler()
        };
        var factory = new TaskHandlerFactory(handlers);

        // Act
        var handler = factory.GetHandler("Procurement");

        // Assert
        Assert.NotNull(handler);
        Assert.IsType<ProcurementTaskHandler>(handler);
    }

    [Fact]
    public void GetHandler_CaseInsensitive_ShouldWork()
    {
        // Arrange
        var handlers = new ITaskHandler[] { new DevelopmentTaskHandler() };
        var factory = new TaskHandlerFactory(handlers);

        // Act
        var result1 = factory.GetHandler("Development");
        var result2 = factory.GetHandler("development");
        var result3 = factory.GetHandler("DEVELOPMENT");

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);
    }

    [Fact]
    public void GetHandler_WithUnknownType_ShouldReturnNull()
    {
        // Arrange
        var handlers = new ITaskHandler[] { new ProcurementTaskHandler() };
        var factory = new TaskHandlerFactory(handlers);

        // Act
        var handler = factory.GetHandler("Unknown");

        // Assert
        Assert.Null(handler);
    }

    [Fact]
    public void HasHandler_WithValidType_ShouldReturnTrue()
    {
        // Arrange
        var handlers = new ITaskHandler[] { new ProcurementTaskHandler() };
        var factory = new TaskHandlerFactory(handlers);

        // Act
        var result = factory.HasHandler("Procurement");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasHandler_WithInvalidType_ShouldReturnFalse()
    {
        // Arrange
        var handlers = new ITaskHandler[] { new DevelopmentTaskHandler() };
        var factory = new TaskHandlerFactory(handlers);

        // Act
        var result = factory.HasHandler("Unknown");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetRegisteredTaskTypes_ShouldReturnAllTypes()
    {
        // Arrange
        var handlers = new ITaskHandler[] 
        { 
            new ProcurementTaskHandler(),
            new DevelopmentTaskHandler()
        };
        var factory = new TaskHandlerFactory(handlers);

        // Act
        var types = factory.GetRegisteredTaskTypes();

        // Assert
        Assert.Contains("Procurement", types);
        Assert.Contains("Development", types);
        Assert.Equal(2, types.Count());
    }
}

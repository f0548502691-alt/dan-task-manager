using DanTaskManager.Services;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace DanTaskManager.Tests;

public class TaskTypeValidationServiceTests
{
    [Fact]
    public void HasTaskType_UsesCaseInsensitiveConfiguredTypes_AndLastDuplicateDefinition()
    {
        var service = CreateService(
            new TaskTypeDefinition { TaskType = "Support", FinalStatus = 2 },
            new TaskTypeDefinition { TaskType = "   ", FinalStatus = 99 },
            new TaskTypeDefinition { TaskType = "support", FinalStatus = 4 });

        Assert.True(service.HasTaskType("SUPPORT"));
        Assert.False(service.HasTaskType("   "));
        Assert.Equal(4, service.GetFinalStatus("Support"));
        Assert.Null(service.GetFinalStatus("Unknown"));
    }

    [Fact]
    public void ValidateStatusData_OptionalFieldMayBeMissing_ButPresentValueMustMatchRule()
    {
        var service = CreateService(new TaskTypeDefinition
        {
            TaskType = "Support",
            StatusRules = new List<TaskStatusRuleDefinition>
            {
                new()
                {
                    Status = 1,
                    Fields = new List<FieldRuleDefinition>
                    {
                        new()
                        {
                            Field = "score",
                            Type = "number",
                            Required = false
                        }
                    }
                }
            }
        });

        var missingOptional = service.ValidateStatusData("Support", 1, "{}");
        var wrongType = service.ValidateStatusData("Support", 1, JsonSerializer.Serialize(new { score = "high" }));

        Assert.True(missingOptional.IsValid);
        Assert.False(wrongType.IsValid);
        Assert.Contains("score", wrongType.Message);
    }

    [Theory]
    [InlineData(new[] { "1200", "1150" }, true)]
    [InlineData(new[] { "1200" }, false)]
    [InlineData(new[] { "1200", "" }, false)]
    public void ValidateStatusData_ArrayRules_EnforceLengthAndNonEmptyStringElements(
        string[] prices,
        bool expectedIsValid)
    {
        var service = CreateService(new TaskTypeDefinition
        {
            TaskType = "Procurement",
            StatusRules = new List<TaskStatusRuleDefinition>
            {
                new()
                {
                    Status = 2,
                    Fields = new List<FieldRuleDefinition>
                    {
                        new()
                        {
                            Field = "prices",
                            Type = "array",
                            ArrayLength = 2,
                            ElementType = "string",
                            Required = true
                        }
                    }
                }
            }
        });

        var result = service.ValidateStatusData(
            "Procurement",
            2,
            JsonSerializer.Serialize(new { prices }));

        Assert.Equal(expectedIsValid, result.IsValid);
    }

    [Theory]
    [InlineData("\"1.2.3\"", true)]
    [InlineData("2.5", true)]
    [InlineData("\"1.2.beta\"", false)]
    [InlineData("true", false)]
    public void ValidateStatusData_StringOrNumberSemanticVersion_ValidatesTypeAndPattern(
        string versionJson,
        bool expectedIsValid)
    {
        var service = CreateService(new TaskTypeDefinition
        {
            TaskType = "Development",
            StatusRules = new List<TaskStatusRuleDefinition>
            {
                new()
                {
                    Status = 4,
                    Fields = new List<FieldRuleDefinition>
                    {
                        new()
                        {
                            Field = "versionNumber",
                            Type = "stringOrNumber",
                            Pattern = "semantic_version"
                        }
                    }
                }
            }
        });

        var result = service.ValidateStatusData("Development", 4, $$"""{"versionNumber":{{versionJson}}}""");

        Assert.Equal(expectedIsValid, result.IsValid);
    }

    [Theory]
    [InlineData("feature/support-ticket", true)]
    [InlineData("feature//support-ticket", false)]
    [InlineData("release.", false)]
    [InlineData("hotfix ticket", false)]
    public void ValidateStatusData_GitBranchPattern_RejectsUnsafeBranchNames(
        string branchName,
        bool expectedIsValid)
    {
        var service = CreateService(new TaskTypeDefinition
        {
            TaskType = "Development",
            StatusRules = new List<TaskStatusRuleDefinition>
            {
                new()
                {
                    Status = 3,
                    Fields = new List<FieldRuleDefinition>
                    {
                        new()
                        {
                            Field = "branchName",
                            Type = "string",
                            Pattern = "valid_git_branch"
                        }
                    }
                }
            }
        });

        var result = service.ValidateStatusData(
            "Development",
            3,
            JsonSerializer.Serialize(new { branchName }));

        Assert.Equal(expectedIsValid, result.IsValid);
    }

    [Fact]
    public void ValidateStatusData_CustomRegexPattern_RejectsNonMatchingValues()
    {
        var service = CreateService(new TaskTypeDefinition
        {
            TaskType = "Support",
            StatusRules = new List<TaskStatusRuleDefinition>
            {
                new()
                {
                    Status = 1,
                    Fields = new List<FieldRuleDefinition>
                    {
                        new()
                        {
                            Field = "ticketCode",
                            Type = "string",
                            Pattern = "^SUP-[0-9]{4}$"
                        }
                    }
                }
            }
        });

        var valid = service.ValidateStatusData("Support", 1, JsonSerializer.Serialize(new { ticketCode = "SUP-1234" }));
        var invalid = service.ValidateStatusData("Support", 1, JsonSerializer.Serialize(new { ticketCode = "BUG-1234" }));

        Assert.True(valid.IsValid);
        Assert.False(invalid.IsValid);
        Assert.Contains("ticketCode", invalid.Message);
    }

    private static TaskTypeValidationService CreateService(params TaskTypeDefinition[] taskTypes)
    {
        return new TaskTypeValidationService(Options.Create(new TaskTypeValidationOptions
        {
            TaskTypes = taskTypes.ToList()
        }));
    }
}

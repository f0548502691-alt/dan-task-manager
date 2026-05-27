using DanTaskManager.Domain;
using DanTaskManager.Domain.Handlers;
using DanTaskManager.Services;

namespace DanTaskManager.Tests;

/// <summary>
/// Unit tests for <see cref="TaskTypeConflictValidator"/>.
/// </summary>
public class TaskTypeConflictValidatorTests
{
    [Fact]
    public void DetectConflicts_NoOverlap_ReturnsEmpty()
    {
        var providers = new ITaskWorkflowRuleProvider[]
        {
            new FakeProvider("Metadata", 0, new[] { "Procurement", "Development" }),
            new FakeProvider("Handler", 100, new[] { "Analysis", "Testing" })
        };

        var conflicts = TaskTypeConflictValidator.DetectConflicts(providers);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void DetectConflicts_OverlappingTaskType_ReportsConflict()
    {
        var providers = new ITaskWorkflowRuleProvider[]
        {
            new FakeProvider("Metadata", 0, new[] { "Procurement", "Marketing" }),
            new FakeProvider("Handler", 100, new[] { "Marketing", "Analysis" })
        };

        var conflicts = TaskTypeConflictValidator.DetectConflicts(providers);

        var conflict = Assert.Single(conflicts);
        Assert.Equal("Marketing", conflict.TaskType);
        Assert.Equal(new[] { "Metadata", "Handler" }, conflict.Sources);
    }

    [Fact]
    public void DetectConflicts_CaseInsensitiveTaskTypeMatch()
    {
        var providers = new ITaskWorkflowRuleProvider[]
        {
            new FakeProvider("Metadata", 0, new[] { "procurement" }),
            new FakeProvider("Handler", 100, new[] { "Procurement" })
        };

        var conflicts = TaskTypeConflictValidator.DetectConflicts(providers);

        Assert.Single(conflicts);
    }

    [Fact]
    public void DetectConflicts_IgnoresBlankTaskTypeCodes()
    {
        var providers = new ITaskWorkflowRuleProvider[]
        {
            new FakeProvider("Metadata", 0, new[] { "Procurement", "" }),
            new FakeProvider("Handler", 100, new[] { "Analysis", " " })
        };

        var conflicts = TaskTypeConflictValidator.DetectConflicts(providers);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void DetectConflicts_OrderedByPriority_LowestPriorityListedFirst()
    {
        var providers = new ITaskWorkflowRuleProvider[]
        {
            new FakeProvider("Handler", 100, new[] { "Shared" }),
            new FakeProvider("Metadata", 0, new[] { "Shared" })
        };

        var conflicts = TaskTypeConflictValidator.DetectConflicts(providers);

        var conflict = Assert.Single(conflicts);
        Assert.Equal("Metadata", conflict.Sources[0]);
        Assert.Equal("Handler", conflict.Sources[1]);
    }

    private sealed class FakeProvider : ITaskWorkflowRuleProvider
    {
        private readonly string[] _taskTypes;

        public FakeProvider(string source, int priority, IEnumerable<string> taskTypes)
        {
            SourceName = source;
            Priority = priority;
            _taskTypes = taskTypes.ToArray();
        }

        public string SourceName { get; }
        public int Priority { get; }

        public bool CanHandle(string taskType) => _taskTypes.Contains(taskType, StringComparer.OrdinalIgnoreCase);
        public int? GetFinalStatus(string taskType) => 3;
        public ValidationResult ValidateStatusChange(BaseTask task, int nextStatus, string newDataJson) => ValidationResult.Success();
        public ValidationResult ValidateClose(BaseTask task, string finalNotes, string closeDataJson) => ValidationResult.Success();
        public string BuildCloseData(BaseTask task, string finalNotes) => task.CustomDataJson;
        public IReadOnlyCollection<string> GetKnownTaskTypes() => _taskTypes;
    }
}

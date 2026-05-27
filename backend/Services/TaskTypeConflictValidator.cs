using Microsoft.Extensions.Hosting;

namespace DanTaskManager.Services;

/// <summary>
/// Configuration for <see cref="TaskTypeConflictValidator"/>.
/// </summary>
public class TaskTypeConflictValidatorOptions
{
    public const string SectionName = "TaskTypeConflictValidation";

    /// <summary>
    /// When true, the application fails to start if the same task-type code is claimed by more than
    /// one rule provider (metadata + code-backed handler). When false (default), a warning is logged
    /// and the higher-priority provider wins at runtime.
    /// </summary>
    public bool FailOnConflict { get; set; }
}

/// <summary>
/// Startup diagnostic that scans every registered <see cref="ITaskWorkflowRuleProvider"/> and
/// reports task-type codes claimed by more than one source. Prevents silent shadowing between
/// metadata-driven and code-backed handlers.
/// </summary>
public class TaskTypeConflictValidator : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TaskTypeConflictValidatorOptions _options;
    private readonly ILogger<TaskTypeConflictValidator> _logger;

    public TaskTypeConflictValidator(
        IServiceProvider serviceProvider,
        Microsoft.Extensions.Options.IOptions<TaskTypeConflictValidatorOptions> options,
        ILogger<TaskTypeConflictValidator> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var providers = scope.ServiceProvider
            .GetServices<ITaskWorkflowRuleProvider>()
            .OrderBy(provider => provider.Priority)
            .ToArray();

        if (providers.Length < 2)
        {
            return Task.CompletedTask;
        }

        var conflicts = DetectConflicts(providers);
        if (conflicts.Count == 0)
        {
            _logger.LogInformation(
                "Task-type rule providers registered ({Count}) with no overlapping task types.",
                providers.Length);
            return Task.CompletedTask;
        }

        foreach (var conflict in conflicts)
        {
            var sources = string.Join(", ", conflict.Sources);
            var winner = conflict.Sources.First();
            _logger.LogWarning(
                "Task type '{TaskType}' is claimed by multiple rule providers ({Sources}). " +
                "The '{Winner}' provider (lowest Priority) will be used at runtime; others are shadowed.",
                conflict.TaskType,
                sources,
                winner);
        }

        if (_options.FailOnConflict)
        {
            var summary = string.Join("; ", conflicts.Select(c => $"{c.TaskType}=[{string.Join(",", c.Sources)}]"));
            throw new InvalidOperationException(
                $"Task-type conflicts detected between rule providers: {summary}. " +
                "Resolve by removing the duplicate registration or disable TaskTypeConflictValidation:FailOnConflict.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static IReadOnlyList<TaskTypeConflict> DetectConflicts(IEnumerable<ITaskWorkflowRuleProvider> providers)
    {
        var ordered = providers.OrderBy(provider => provider.Priority).ToArray();
        var sourcesByTaskType = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in ordered)
        {
            foreach (var taskType in provider.GetKnownTaskTypes())
            {
                if (string.IsNullOrWhiteSpace(taskType))
                {
                    continue;
                }

                if (!sourcesByTaskType.TryGetValue(taskType, out var sources))
                {
                    sources = new List<string>();
                    sourcesByTaskType[taskType] = sources;
                }

                if (!sources.Contains(provider.SourceName, StringComparer.OrdinalIgnoreCase))
                {
                    sources.Add(provider.SourceName);
                }
            }
        }

        return sourcesByTaskType
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => new TaskTypeConflict(pair.Key, pair.Value))
            .OrderBy(conflict => conflict.TaskType, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

internal record TaskTypeConflict(string TaskType, IReadOnlyList<string> Sources);

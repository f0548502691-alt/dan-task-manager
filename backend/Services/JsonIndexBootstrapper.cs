using DanTaskManager.Data;
using DanTaskManager.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace DanTaskManager.Services;

/// <summary>
/// At startup, materializes a SQL Server JSON_VALUE-backed computed column
/// and a non-unique index for every <see cref="TaskFieldDefinition"/> marked
/// <c>IsIndexed = true</c>. Without this, the <c>IsIndexed</c> flag is purely
/// declarative; with it, queries that filter custom JSON fields can use a
/// real index instead of scanning every row.
///
/// Scope:
/// - Only runs against SQL Server providers. The InMemory provider used in
///   tests is skipped because it cannot evaluate raw SQL or computed columns.
/// - Only indexes scalar fields (<c>string</c>, <c>number</c>, <c>stringOrNumber</c>).
///   Arrays and objects need a different strategy and are deliberately ignored.
/// - Idempotent: existing columns/indexes are detected and skipped.
/// </summary>
public class JsonIndexBootstrapper : IHostedService
{
    private const int MaxIndexedStringLength = 450;

    private static readonly HashSet<string> IndexableScalarTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "string",
        "number",
        "stringOrNumber",
    };

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JsonIndexBootstrapper> _logger;

    public JsonIndexBootstrapper(
        IServiceProvider serviceProvider,
        ILogger<JsonIndexBootstrapper> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!context.Database.IsSqlServer())
        {
            _logger.LogInformation(
                "JsonIndexBootstrapper skipped: provider is {Provider}, only SQL Server is supported.",
                context.Database.ProviderName);
            return;
        }

        var indexedFields = await context.TaskFieldDefinitions
            .AsNoTracking()
            .Where(field => field.IsIndexed)
            .Include(field => field.TaskTypeMetadata)
            .ToListAsync(cancellationToken);

        foreach (var field in indexedFields)
        {
            if (!IndexableScalarTypes.Contains(field.DataType))
            {
                _logger.LogDebug(
                    "Skipping non-scalar indexed field '{FieldKey}' on task type '{TaskType}' (type={DataType})",
                    field.FieldKey,
                    field.TaskTypeMetadata?.Code,
                    field.DataType);
                continue;
            }

            await EnsureIndexAsync(context, field, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureIndexAsync(
        ApplicationDbContext context,
        TaskFieldDefinition field,
        CancellationToken cancellationToken)
    {
        var columnName = BuildColumnName(field.FieldKey);
        var indexName = $"IX_Tasks_{columnName}";
        var jsonPath = $"$.{field.FieldKey}";
        var sql = new StringBuilder();

        sql.AppendLine($"IF COL_LENGTH('[Tasks]', '{columnName}') IS NULL");
        sql.AppendLine("BEGIN");
        sql.AppendLine(
            $"    ALTER TABLE [Tasks] ADD [{columnName}] AS " +
            $"CAST(JSON_VALUE([CustomDataJson], '{jsonPath}') AS NVARCHAR({MaxIndexedStringLength}));");
        sql.AppendLine("END;");
        sql.AppendLine();
        sql.AppendLine($"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{indexName}' AND object_id = OBJECT_ID('[Tasks]'))");
        sql.AppendLine("BEGIN");
        sql.AppendLine(
            $"    CREATE INDEX [{indexName}] ON [Tasks]([TaskType], [{columnName}]) " +
            "WHERE [CustomDataJson] IS NOT NULL;");
        sql.AppendLine("END;");

        try
        {
            await context.Database.ExecuteSqlRawAsync(sql.ToString(), cancellationToken);
            _logger.LogInformation(
                "JSON index ready for task type '{TaskType}' field '{FieldKey}' (column [{Column}], index [{Index}])",
                field.TaskTypeMetadata?.Code,
                field.FieldKey,
                columnName,
                indexName);
        }
        catch (Exception ex)
        {
            // Indexing is a performance optimization, not a correctness guarantee.
            // Failing here would prevent the app from starting, which is worse than
            // running with table scans on that field.
            _logger.LogWarning(
                ex,
                "Failed to ensure JSON index for task type '{TaskType}' field '{FieldKey}'; the app will continue without it.",
                field.TaskTypeMetadata?.Code,
                field.FieldKey);
        }
    }

    internal static string BuildColumnName(string fieldKey)
    {
        var sanitized = new StringBuilder("cd_", fieldKey.Length + 3);
        foreach (var ch in fieldKey)
        {
            sanitized.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }
        return sanitized.ToString();
    }
}

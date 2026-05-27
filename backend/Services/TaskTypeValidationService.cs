using DanTaskManager.Data;
using DanTaskManager.Domain;
using DanTaskManager.Domain.Handlers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DanTaskManager.Services;

public interface ITaskTypeValidationService
{
    bool HasTaskType(string taskType);
    int? GetFinalStatus(string taskType);
    ValidationResult ValidateStatusData(string taskType, int status, string payloadJson);
}

public interface ITaskTypeMetadataService
{
    IReadOnlyCollection<TaskTypeSchemaDto> GetTaskTypes();
    TaskTypeSchemaDto? GetTaskType(string taskType);
    MetadataOperationResult UpsertTaskType(UpsertTaskTypeCommand command);
    MetadataOperationResult UpsertFieldDefinition(string taskType, UpsertFieldDefinitionCommand command);
}

public record UpsertTaskTypeCommand(
    string TaskType,
    string? DisplayName,
    int? FinalStatus,
    bool IsActive = true);

public class UpsertFieldDefinitionCommand
{
    public string Field { get; init; } = string.Empty;
    public string Type { get; init; } = "string";
    public bool Required { get; init; } = true;
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public decimal? MinValue { get; init; }
    public decimal? MaxValue { get; init; }
    public int? ArrayLength { get; init; }
    public int? MinItems { get; init; }
    public int? MaxItems { get; init; }
    public string? ElementType { get; init; }
    public string? Pattern { get; init; }
    public int? AppliesFromStatus { get; init; }
    public int? AppliesToStatus { get; init; }
    public List<string>? AllowedValues { get; init; }
    public bool IsIndexed { get; init; }
}

public class MetadataOperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public TaskTypeSchemaDto? TaskType { get; init; }

    public static MetadataOperationResult SuccessResult(TaskTypeSchemaDto taskType, string message = "")
        => new() { Success = true, Message = message, TaskType = taskType };

    public static MetadataOperationResult FailureResult(string message)
        => new() { Success = false, Message = message };
}

public class TaskTypeSchemaDto
{
    public string TaskType { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int? FinalStatus { get; init; }
    public bool IsActive { get; init; }
    public int Version { get; init; }
    public IReadOnlyList<FieldRuleDefinition> Fields { get; init; } = Array.Empty<FieldRuleDefinition>();
}

public class TaskTypeValidationOptions
{
    public const string SectionName = "TaskTypeValidation";

    public List<TaskTypeDefinition> TaskTypes { get; set; } = [];
}

public class TaskTypeDefinition
{
    public string TaskType { get; set; } = string.Empty;
    public int? FinalStatus { get; set; }
    public List<TaskStatusRuleDefinition> StatusRules { get; set; } = [];
    public List<FieldRuleDefinition> FieldRules { get; set; } = [];
}

public class TaskStatusRuleDefinition
{
    public int Status { get; set; }
    public List<FieldRuleDefinition> Fields { get; set; } = [];
}

public class FieldRuleDefinition
{
    public string Field { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; } = true;
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public int? ArrayLength { get; set; }
    public int? MinItems { get; set; }
    public int? MaxItems { get; set; }
    public string? ElementType { get; set; }
    public string? Pattern { get; set; }
    public int? AppliesFromStatus { get; set; }
    public int? AppliesToStatus { get; set; }
    public List<string>? AllowedValues { get; set; }
    public bool IsIndexed { get; set; }
}

public class TaskTypeValidationService : ITaskTypeValidationService, ITaskTypeMetadataService
{
    private const string CachePrefix = "task-type-validation::";

    private readonly ApplicationDbContext? _context;
    private readonly IMemoryCache? _cache;
    private readonly IReadOnlyDictionary<string, TaskTypeDefinition>? _inMemoryTaskTypeMap;

    public TaskTypeValidationService(
        ApplicationDbContext context,
        IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public TaskTypeValidationService(IOptions<TaskTypeValidationOptions> options)
    {
        var configTaskTypes = options.Value.TaskTypes ?? [];

        _inMemoryTaskTypeMap = configTaskTypes
            .Where(definition => !string.IsNullOrWhiteSpace(definition.TaskType))
            .GroupBy(definition => definition.TaskType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
    }

    public bool HasTaskType(string taskType)
    {
        return GetTaskDefinition(taskType) != null;
    }

    public int? GetFinalStatus(string taskType)
    {
        return GetTaskDefinition(taskType)?.FinalStatus;
    }

    public ValidationResult ValidateStatusData(string taskType, int status, string payloadJson)
    {
        var taskDefinition = GetTaskDefinition(taskType);
        if (taskDefinition == null)
        {
            return ValidationResult.Success();
        }

        var fieldRules = ResolveFieldRules(taskDefinition, status);
        if (fieldRules.Count == 0)
        {
            return ValidationResult.Success();
        }

        try
        {
            using var jsonDoc = JsonDocument.Parse(payloadJson);
            var root = jsonDoc.RootElement;

            foreach (var fieldRule in fieldRules)
            {
                var fieldResult = ValidateField(root, status, fieldRule);
                if (!fieldResult.IsValid)
                {
                    return fieldResult;
                }
            }
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"Invalid JSON payload: {ex.Message}");
        }

        return ValidationResult.Success();
    }

    public IReadOnlyCollection<TaskTypeSchemaDto> GetTaskTypes()
    {
        if (_context == null)
        {
            return (_inMemoryTaskTypeMap ?? new Dictionary<string, TaskTypeDefinition>(StringComparer.OrdinalIgnoreCase))
                .Values
                .Select(MapToSchemaDto)
                .OrderBy(schema => schema.TaskType, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return _context.TaskTypes
            .AsNoTracking()
            .Include(taskType => taskType.FieldDefinitions)
            .OrderBy(taskType => taskType.Code)
            .ToList()
            .Select(MapToSchemaDto)
            .ToList();
    }

    public TaskTypeSchemaDto? GetTaskType(string taskType)
    {
        if (string.IsNullOrWhiteSpace(taskType))
        {
            return null;
        }

        var normalizedTaskType = taskType.Trim();
        var normalizedTaskTypeLower = normalizedTaskType.ToLowerInvariant();

        if (_context == null)
        {
            if (_inMemoryTaskTypeMap == null ||
                !_inMemoryTaskTypeMap.TryGetValue(normalizedTaskType, out var definition))
            {
                return null;
            }

            return MapToSchemaDto(definition);
        }

        var taskTypeEntity = _context.TaskTypes
            .AsNoTracking()
            .Include(item => item.FieldDefinitions)
            .FirstOrDefault(item => item.Code.ToLower() == normalizedTaskTypeLower);

        return taskTypeEntity == null ? null : MapToSchemaDto(taskTypeEntity);
    }

    public MetadataOperationResult UpsertTaskType(UpsertTaskTypeCommand command)
    {
        if (_context == null)
        {
            return MetadataOperationResult.FailureResult("Database-backed metadata updates are not available");
        }

        var code = command.TaskType.Trim();
        var codeLower = code.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            return MetadataOperationResult.FailureResult("TaskType is required");
        }

        if (!command.FinalStatus.HasValue)
        {
            return MetadataOperationResult.FailureResult("FinalStatus is required");
        }

        if (command.FinalStatus.Value < WorkflowConstants.CreatedStatus)
        {
            return MetadataOperationResult.FailureResult(
                $"FinalStatus must be greater than or equal to {WorkflowConstants.CreatedStatus}");
        }

        if (command.FinalStatus.Value >= WorkflowConstants.ClosedStatus)
        {
            return MetadataOperationResult.FailureResult(
                $"FinalStatus must be less than {WorkflowConstants.ClosedStatus}");
        }

        var entity = _context.TaskTypes
            .Include(taskType => taskType.FieldDefinitions)
            .FirstOrDefault(taskType => taskType.Code.ToLower() == codeLower);

        if (entity == null)
        {
            entity = new TaskTypeMetadata
            {
                Code = code,
                DisplayName = string.IsNullOrWhiteSpace(command.DisplayName) ? code : command.DisplayName.Trim(),
                FinalStatus = command.FinalStatus.Value,
                IsActive = command.IsActive,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.TaskTypes.Add(entity);
        }
        else
        {
            entity.DisplayName = string.IsNullOrWhiteSpace(command.DisplayName) ? entity.DisplayName : command.DisplayName.Trim();
            entity.FinalStatus = command.FinalStatus.Value;
            entity.IsActive = command.IsActive;
            entity.Version += 1;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        _context.SaveChanges();
        RemoveCacheEntry(code);

        var reloaded = _context.TaskTypes
            .AsNoTracking()
            .Include(taskType => taskType.FieldDefinitions)
            .First(taskType => taskType.Code.ToLower() == codeLower);

        return MetadataOperationResult.SuccessResult(
            MapToSchemaDto(reloaded),
            "Task type metadata saved");
    }

    public MetadataOperationResult UpsertFieldDefinition(string taskType, UpsertFieldDefinitionCommand command)
    {
        if (_context == null)
        {
            return MetadataOperationResult.FailureResult("Database-backed metadata updates are not available");
        }

        if (string.IsNullOrWhiteSpace(taskType))
        {
            return MetadataOperationResult.FailureResult("TaskType is required");
        }

        var normalizedTaskType = taskType.Trim();
        var normalizedTaskTypeLower = normalizedTaskType.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(command.Field))
        {
            return MetadataOperationResult.FailureResult("Field is required");
        }

        if (command.AppliesFromStatus.HasValue &&
            command.AppliesToStatus.HasValue &&
            command.AppliesFromStatus.Value > command.AppliesToStatus.Value)
        {
            return MetadataOperationResult.FailureResult("AppliesFromStatus must be less than or equal to AppliesToStatus");
        }

        if (command.AppliesFromStatus.HasValue && command.AppliesFromStatus.Value < WorkflowConstants.CreatedStatus)
        {
            return MetadataOperationResult.FailureResult(
                $"AppliesFromStatus must be greater than or equal to {WorkflowConstants.CreatedStatus}");
        }

        if (command.AppliesToStatus.HasValue && command.AppliesToStatus.Value < WorkflowConstants.CreatedStatus)
        {
            return MetadataOperationResult.FailureResult(
                $"AppliesToStatus must be greater than or equal to {WorkflowConstants.CreatedStatus}");
        }

        var taskTypeEntity = _context.TaskTypes
            .Include(item => item.FieldDefinitions)
            .FirstOrDefault(item => item.Code.ToLower() == normalizedTaskTypeLower);

        if (taskTypeEntity == null)
        {
            return MetadataOperationResult.FailureResult($"Task type '{taskType}' not found");
        }

        if (taskTypeEntity.FinalStatus.HasValue &&
            command.AppliesFromStatus.HasValue &&
            command.AppliesFromStatus.Value > taskTypeEntity.FinalStatus.Value)
        {
            return MetadataOperationResult.FailureResult(
                $"AppliesFromStatus cannot be greater than FinalStatus ({taskTypeEntity.FinalStatus.Value})");
        }

        if (taskTypeEntity.FinalStatus.HasValue &&
            command.AppliesToStatus.HasValue &&
            command.AppliesToStatus.Value > taskTypeEntity.FinalStatus.Value)
        {
            return MetadataOperationResult.FailureResult(
                $"AppliesToStatus cannot be greater than FinalStatus ({taskTypeEntity.FinalStatus.Value})");
        }

        var fieldKey = command.Field.Trim();
        var fieldDefinition = taskTypeEntity.FieldDefinitions
            .FirstOrDefault(item => item.FieldKey.Equals(fieldKey, StringComparison.OrdinalIgnoreCase));

        if (fieldDefinition == null)
        {
            fieldDefinition = new TaskFieldDefinition
            {
                TaskTypeMetadataId = taskTypeEntity.Id,
                FieldKey = fieldKey,
                CreatedAt = DateTime.UtcNow
            };
            taskTypeEntity.FieldDefinitions.Add(fieldDefinition);
        }

        fieldDefinition.DataType = string.IsNullOrWhiteSpace(command.Type) ? "string" : command.Type.Trim();
        fieldDefinition.IsRequired = command.Required;
        fieldDefinition.MinLength = command.MinLength;
        fieldDefinition.MaxLength = command.MaxLength;
        fieldDefinition.MinValue = command.MinValue;
        fieldDefinition.MaxValue = command.MaxValue;
        fieldDefinition.ArrayLength = command.ArrayLength;
        fieldDefinition.MinItems = command.MinItems;
        fieldDefinition.MaxItems = command.MaxItems;
        fieldDefinition.ElementType = command.ElementType;
        fieldDefinition.RegexPattern = command.Pattern;
        fieldDefinition.AppliesFromStatus = command.AppliesFromStatus;
        fieldDefinition.AppliesToStatus = command.AppliesToStatus;
        fieldDefinition.IsIndexed = command.IsIndexed;
        fieldDefinition.AllowedValuesJson = command.AllowedValues is { Count: > 0 }
            ? JsonSerializer.Serialize(command.AllowedValues)
            : null;
        fieldDefinition.UpdatedAt = DateTime.UtcNow;

        taskTypeEntity.Version += 1;
        taskTypeEntity.UpdatedAt = DateTime.UtcNow;

        _context.SaveChanges();
        RemoveCacheEntry(taskTypeEntity.Code);

        var reloaded = _context.TaskTypes
            .AsNoTracking()
            .Include(item => item.FieldDefinitions)
            .First(item => item.Id == taskTypeEntity.Id);

        return MetadataOperationResult.SuccessResult(
            MapToSchemaDto(reloaded),
            "Field definition saved");
    }

    private TaskTypeDefinition? GetTaskDefinition(string taskType)
    {
        if (string.IsNullOrWhiteSpace(taskType))
        {
            return null;
        }

        if (_inMemoryTaskTypeMap != null)
        {
            _inMemoryTaskTypeMap.TryGetValue(taskType, out var inMemoryDefinition);
            return inMemoryDefinition;
        }

        if (_context == null || _cache == null)
        {
            return null;
        }

        var cacheKey = $"{CachePrefix}{taskType.Trim().ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out TaskTypeDefinition? cachedDefinition))
        {
            return cachedDefinition;
        }

        var normalizedTaskType = taskType.Trim();
        var normalizedTaskTypeLower = normalizedTaskType.ToLowerInvariant();

        var entity = _context.TaskTypes
            .AsNoTracking()
            .Include(item => item.FieldDefinitions)
            .FirstOrDefault(item => item.Code.ToLower() == normalizedTaskTypeLower && item.IsActive);

        if (entity == null)
        {
            return null;
        }

        var mappedDefinition = MapToDefinition(entity);
        _cache.Set(cacheKey, mappedDefinition, TimeSpan.FromMinutes(5));
        return mappedDefinition;
    }

    private static TaskTypeDefinition MapToDefinition(TaskTypeMetadata entity)
    {
        var fieldRules = entity.FieldDefinitions
            .OrderBy(field => field.FieldKey, StringComparer.OrdinalIgnoreCase)
            .Select(field => new FieldRuleDefinition
            {
                Field = field.FieldKey,
                Type = field.DataType,
                Required = field.IsRequired,
                MinLength = field.MinLength,
                MaxLength = field.MaxLength,
                MinValue = field.MinValue,
                MaxValue = field.MaxValue,
                ArrayLength = field.ArrayLength,
                MinItems = field.MinItems,
                MaxItems = field.MaxItems,
                ElementType = field.ElementType,
                Pattern = field.RegexPattern,
                AppliesFromStatus = field.AppliesFromStatus,
                AppliesToStatus = field.AppliesToStatus,
                AllowedValues = ParseAllowedValues(field.AllowedValuesJson),
                IsIndexed = field.IsIndexed
            })
            .ToList();

        return new TaskTypeDefinition
        {
            TaskType = entity.Code,
            FinalStatus = entity.FinalStatus,
            FieldRules = fieldRules
        };
    }

    private static TaskTypeSchemaDto MapToSchemaDto(TaskTypeMetadata entity)
    {
        return new TaskTypeSchemaDto
        {
            TaskType = entity.Code,
            DisplayName = entity.DisplayName,
            FinalStatus = entity.FinalStatus,
            IsActive = entity.IsActive,
            Version = entity.Version,
            Fields = entity.FieldDefinitions
                .OrderBy(field => field.FieldKey, StringComparer.OrdinalIgnoreCase)
                .Select(field => new FieldRuleDefinition
                {
                    Field = field.FieldKey,
                    Type = field.DataType,
                    Required = field.IsRequired,
                    MinLength = field.MinLength,
                    MaxLength = field.MaxLength,
                    MinValue = field.MinValue,
                    MaxValue = field.MaxValue,
                    ArrayLength = field.ArrayLength,
                    MinItems = field.MinItems,
                    MaxItems = field.MaxItems,
                    ElementType = field.ElementType,
                    Pattern = field.RegexPattern,
                    AppliesFromStatus = field.AppliesFromStatus,
                    AppliesToStatus = field.AppliesToStatus,
                    AllowedValues = ParseAllowedValues(field.AllowedValuesJson),
                    IsIndexed = field.IsIndexed
                })
                .ToList()
        };
    }

    private static TaskTypeSchemaDto MapToSchemaDto(TaskTypeDefinition definition)
    {
        return new TaskTypeSchemaDto
        {
            TaskType = definition.TaskType,
            DisplayName = definition.TaskType,
            FinalStatus = definition.FinalStatus,
            IsActive = true,
            Version = 1,
            Fields = definition.FieldRules.Count > 0
                ? definition.FieldRules
                : definition.StatusRules.SelectMany(rule =>
                    rule.Fields.Select(field => new FieldRuleDefinition
                    {
                        Field = field.Field,
                        Type = field.Type,
                        Required = field.Required,
                        MinLength = field.MinLength,
                        MaxLength = field.MaxLength,
                        MinValue = field.MinValue,
                        MaxValue = field.MaxValue,
                        ArrayLength = field.ArrayLength,
                        MinItems = field.MinItems,
                        MaxItems = field.MaxItems,
                        ElementType = field.ElementType,
                        Pattern = field.Pattern,
                        AppliesFromStatus = field.AppliesFromStatus ?? rule.Status,
                        AppliesToStatus = field.AppliesToStatus ?? rule.Status,
                        AllowedValues = field.AllowedValues,
                        IsIndexed = field.IsIndexed
                    }))
                .ToList()
        };
    }

    private void RemoveCacheEntry(string taskType)
    {
        if (_cache == null || string.IsNullOrWhiteSpace(taskType))
        {
            return;
        }

        var cacheKey = $"{CachePrefix}{taskType.Trim().ToLowerInvariant()}";
        _cache.Remove(cacheKey);
    }

    private static IReadOnlyList<FieldRuleDefinition> ResolveFieldRules(TaskTypeDefinition taskDefinition, int status)
    {
        var explicitRules = taskDefinition.FieldRules.Count > 0
            ? taskDefinition.FieldRules
            : taskDefinition.StatusRules
                .SelectMany(rule => rule.Fields.Select(field => new FieldRuleDefinition
                {
                    Field = field.Field,
                    Type = field.Type,
                    Required = field.Required,
                    MinLength = field.MinLength,
                    MaxLength = field.MaxLength,
                    MinValue = field.MinValue,
                    MaxValue = field.MaxValue,
                    ArrayLength = field.ArrayLength,
                    MinItems = field.MinItems,
                    MaxItems = field.MaxItems,
                    ElementType = field.ElementType,
                    Pattern = field.Pattern,
                    AppliesFromStatus = field.AppliesFromStatus ?? rule.Status,
                    AppliesToStatus = field.AppliesToStatus ?? rule.Status,
                    AllowedValues = field.AllowedValues,
                    IsIndexed = field.IsIndexed
                }))
                .ToList();

        return explicitRules
            .Where(rule => IsRuleApplicableForStatus(rule, status))
            .ToList();
    }

    private static bool IsRuleApplicableForStatus(FieldRuleDefinition rule, int status)
    {
        var from = rule.AppliesFromStatus ?? int.MinValue;
        var to = rule.AppliesToStatus ?? int.MaxValue;
        return status >= from && status <= to;
    }

    private static ValidationResult ValidateField(
        JsonElement root,
        int status,
        FieldRuleDefinition fieldRule)
    {
        if (string.IsNullOrWhiteSpace(fieldRule.Field))
        {
            return ValidationResult.Failure("Invalid field rule definition found (missing Field)");
        }

        if (!root.TryGetProperty(fieldRule.Field, out var fieldElement))
        {
            if (!fieldRule.Required)
            {
                return ValidationResult.Success();
            }

            return ValidationResult.Failure(
                $"Status {status} requires field '{fieldRule.Field}'");
        }

        if (!IsTypeMatch(fieldElement, fieldRule.Type))
        {
            var expectedType = string.IsNullOrWhiteSpace(fieldRule.Type) ? "any" : fieldRule.Type;
            return ValidationResult.Failure(
                $"Field '{fieldRule.Field}' must be of type '{expectedType}'");
        }

        if (fieldElement.ValueKind == JsonValueKind.String)
        {
            var stringValue = fieldElement.GetString() ?? string.Empty;

            if (fieldRule.Required && string.IsNullOrWhiteSpace(stringValue))
            {
                return ValidationResult.Failure($"Field '{fieldRule.Field}' cannot be empty");
            }

            if (fieldRule.MinLength.HasValue && stringValue.Length < fieldRule.MinLength.Value)
            {
                return ValidationResult.Failure(
                    $"Field '{fieldRule.Field}' must contain at least {fieldRule.MinLength.Value} characters");
            }

            if (fieldRule.MaxLength.HasValue && stringValue.Length > fieldRule.MaxLength.Value)
            {
                return ValidationResult.Failure(
                    $"Field '{fieldRule.Field}' cannot contain more than {fieldRule.MaxLength.Value} characters");
            }

            var allowedValuesResult = ValidateAllowedValues(fieldRule, stringValue);
            if (!allowedValuesResult.IsValid)
            {
                return allowedValuesResult;
            }

            var patternResult = ValidatePattern(fieldRule, stringValue);
            if (!patternResult.IsValid)
            {
                return patternResult;
            }
        }

        if (fieldRule.Type.Equals("stringOrNumber", StringComparison.OrdinalIgnoreCase) ||
            fieldElement.ValueKind == JsonValueKind.Number)
        {
            var normalizedValue = GetScalarAsString(fieldElement);
            if (fieldRule.Required && string.IsNullOrWhiteSpace(normalizedValue))
            {
                return ValidationResult.Failure($"Field '{fieldRule.Field}' cannot be empty");
            }

            var patternResult = ValidatePattern(fieldRule, normalizedValue);
            if (!patternResult.IsValid)
            {
                return patternResult;
            }

            var numericResult = ValidateNumericBoundaries(fieldRule, normalizedValue);
            if (!numericResult.IsValid)
            {
                return numericResult;
            }

            var allowedValuesResult = ValidateAllowedValues(fieldRule, normalizedValue);
            if (!allowedValuesResult.IsValid)
            {
                return allowedValuesResult;
            }
        }

        if (fieldElement.ValueKind == JsonValueKind.Array)
        {
            var elements = fieldElement.EnumerateArray().ToList();

            if (fieldRule.ArrayLength.HasValue && elements.Count != fieldRule.ArrayLength.Value)
            {
                return ValidationResult.Failure(
                    $"Field '{fieldRule.Field}' must contain exactly {fieldRule.ArrayLength.Value} items");
            }

            if (fieldRule.MinItems.HasValue && elements.Count < fieldRule.MinItems.Value)
            {
                return ValidationResult.Failure(
                    $"Field '{fieldRule.Field}' must contain at least {fieldRule.MinItems.Value} items");
            }

            if (fieldRule.MaxItems.HasValue && elements.Count > fieldRule.MaxItems.Value)
            {
                return ValidationResult.Failure(
                    $"Field '{fieldRule.Field}' cannot contain more than {fieldRule.MaxItems.Value} items");
            }

            if (!string.IsNullOrWhiteSpace(fieldRule.ElementType))
            {
                foreach (var element in elements)
                {
                    if (!IsTypeMatch(element, fieldRule.ElementType))
                    {
                        return ValidationResult.Failure(
                            $"All items in field '{fieldRule.Field}' must be of type '{fieldRule.ElementType}'");
                    }

                    if (fieldRule.ElementType.Equals("string", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = element.GetString();
                        if (fieldRule.Required && string.IsNullOrWhiteSpace(value))
                        {
                            return ValidationResult.Failure(
                                $"All values in field '{fieldRule.Field}' must be non-empty strings");
                        }
                    }
                }
            }
        }

        return ValidationResult.Success();
    }

    private static ValidationResult ValidateAllowedValues(FieldRuleDefinition fieldRule, string scalarValue)
    {
        if (fieldRule.AllowedValues == null || fieldRule.AllowedValues.Count == 0)
        {
            return ValidationResult.Success();
        }

        if (fieldRule.AllowedValues.Contains(scalarValue, StringComparer.Ordinal))
        {
            return ValidationResult.Success();
        }

        return ValidationResult.Failure(
            $"Field '{fieldRule.Field}' must be one of the allowed values");
    }

    private static ValidationResult ValidateNumericBoundaries(FieldRuleDefinition fieldRule, string normalizedValue)
    {
        if (!fieldRule.MinValue.HasValue && !fieldRule.MaxValue.HasValue)
        {
            return ValidationResult.Success();
        }

        if (!decimal.TryParse(
                normalizedValue,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var numericValue))
        {
            return ValidationResult.Failure(
                $"Field '{fieldRule.Field}' must be numeric to satisfy min/max constraints");
        }

        if (fieldRule.MinValue.HasValue && numericValue < fieldRule.MinValue.Value)
        {
            return ValidationResult.Failure(
                $"Field '{fieldRule.Field}' must be greater than or equal to {fieldRule.MinValue.Value}");
        }

        if (fieldRule.MaxValue.HasValue && numericValue > fieldRule.MaxValue.Value)
        {
            return ValidationResult.Failure(
                $"Field '{fieldRule.Field}' must be less than or equal to {fieldRule.MaxValue.Value}");
        }

        return ValidationResult.Success();
    }

    private static ValidationResult ValidatePattern(FieldRuleDefinition fieldRule, string value)
    {
        if (string.IsNullOrWhiteSpace(fieldRule.Pattern))
        {
            return ValidationResult.Success();
        }

        var patternName = fieldRule.Pattern.Trim();

        if (patternName.Equals("valid_git_branch", StringComparison.OrdinalIgnoreCase))
        {
            if (IsValidGitBranchName(value))
            {
                return ValidationResult.Success();
            }

            return ValidationResult.Failure(
                $"Field '{fieldRule.Field}' is not a valid branch name");
        }

        if (patternName.Equals("semantic_version", StringComparison.OrdinalIgnoreCase))
        {
            if (Regex.IsMatch(value, @"^\d+(\.\d+)*$"))
            {
                return ValidationResult.Success();
            }

            return ValidationResult.Failure(
                $"Field '{fieldRule.Field}' must be in a valid version format (for example: 1.0.0)");
        }

        if (Regex.IsMatch(value, patternName))
        {
            return ValidationResult.Success();
        }

        return ValidationResult.Failure(
            $"Field '{fieldRule.Field}' does not match the required pattern");
    }

    private static bool IsTypeMatch(JsonElement element, string expectedType)
    {
        if (string.IsNullOrWhiteSpace(expectedType))
        {
            return true;
        }

        return expectedType.Trim().ToLowerInvariant() switch
        {
            "string" => element.ValueKind == JsonValueKind.String,
            "number" => element.ValueKind == JsonValueKind.Number,
            "array" => element.ValueKind == JsonValueKind.Array,
            "object" => element.ValueKind == JsonValueKind.Object,
            "boolean" => element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False,
            "stringornumber" => element.ValueKind == JsonValueKind.String || element.ValueKind == JsonValueKind.Number,
            _ => true
        };
    }

    private static bool IsValidGitBranchName(string branchName)
    {
        if (string.IsNullOrWhiteSpace(branchName))
        {
            return false;
        }

        return !branchName.Contains("//", StringComparison.Ordinal) &&
               !branchName.EndsWith("/", StringComparison.Ordinal) &&
               !branchName.EndsWith(".", StringComparison.Ordinal) &&
               !branchName.Contains(' ');
    }

    private static string GetScalarAsString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => element.GetRawText()
        };
    }

    private static List<string>? ParseAllowedValues(string? allowedValuesJson)
    {
        if (string.IsNullOrWhiteSpace(allowedValuesJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(allowedValuesJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

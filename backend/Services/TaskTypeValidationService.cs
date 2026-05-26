using DanTaskManager.Domain.Handlers;
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
    public int? ArrayLength { get; set; }
    public int? MinItems { get; set; }
    public int? MaxItems { get; set; }
    public string? ElementType { get; set; }
    public string? Pattern { get; set; }
}

public class TaskTypeValidationService : ITaskTypeValidationService
{
    private readonly IReadOnlyDictionary<string, TaskTypeDefinition> _taskTypeMap;

    public TaskTypeValidationService(IOptions<TaskTypeValidationOptions> options)
    {
        var configTaskTypes = options.Value.TaskTypes ?? [];

        _taskTypeMap = configTaskTypes
            .Where(definition => !string.IsNullOrWhiteSpace(definition.TaskType))
            .GroupBy(definition => definition.TaskType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
    }

    public bool HasTaskType(string taskType)
    {
        return !string.IsNullOrWhiteSpace(taskType) && _taskTypeMap.ContainsKey(taskType);
    }

    public int? GetFinalStatus(string taskType)
    {
        if (!_taskTypeMap.TryGetValue(taskType, out var definition))
        {
            return null;
        }

        return definition.FinalStatus;
    }

    public ValidationResult ValidateStatusData(string taskType, int status, string payloadJson)
    {
        if (!_taskTypeMap.TryGetValue(taskType, out var taskDefinition))
        {
            return ValidationResult.Success();
        }

        var statusRule = taskDefinition.StatusRules.FirstOrDefault(rule => rule.Status == status);
        if (statusRule == null || statusRule.Fields.Count == 0)
        {
            return ValidationResult.Success();
        }

        try
        {
            using var jsonDoc = JsonDocument.Parse(payloadJson);
            var root = jsonDoc.RootElement;

            foreach (var fieldRule in statusRule.Fields)
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
            return ValidationResult.Failure($"שגיאה בפענוח JSON: {ex.Message}");
        }

        return ValidationResult.Success();
    }

    private static ValidationResult ValidateField(
        JsonElement root,
        int status,
        FieldRuleDefinition fieldRule)
    {
        if (string.IsNullOrWhiteSpace(fieldRule.Field))
        {
            return ValidationResult.Failure("נמצאה חוקיות שדה לא תקינה (Field חסר)");
        }

        if (!root.TryGetProperty(fieldRule.Field, out var fieldElement))
        {
            if (!fieldRule.Required)
            {
                return ValidationResult.Success();
            }

            return ValidationResult.Failure(
                $"בסטטוס {status}, נדרש שדה '{fieldRule.Field}'");
        }

        if (!IsTypeMatch(fieldElement, fieldRule.Type))
        {
            var expectedType = string.IsNullOrWhiteSpace(fieldRule.Type) ? "any" : fieldRule.Type;
            return ValidationResult.Failure(
                $"השדה '{fieldRule.Field}' חייב להיות מסוג '{expectedType}'");
        }

        if (fieldElement.ValueKind == JsonValueKind.String)
        {
            var stringValue = fieldElement.GetString() ?? string.Empty;

            if (fieldRule.Required && string.IsNullOrWhiteSpace(stringValue))
            {
                return ValidationResult.Failure($"השדה '{fieldRule.Field}' לא יכול להיות ריק");
            }

            if (fieldRule.MinLength.HasValue && stringValue.Length < fieldRule.MinLength.Value)
            {
                return ValidationResult.Failure(
                    $"השדה '{fieldRule.Field}' חייב להכיל לפחות {fieldRule.MinLength.Value} תווים");
            }

            if (fieldRule.MaxLength.HasValue && stringValue.Length > fieldRule.MaxLength.Value)
            {
                return ValidationResult.Failure(
                    $"השדה '{fieldRule.Field}' לא יכול להכיל יותר מ-{fieldRule.MaxLength.Value} תווים");
            }

            var patternResult = ValidatePattern(fieldRule, stringValue);
            if (!patternResult.IsValid)
            {
                return patternResult;
            }
        }

        if (fieldRule.Type.Equals("stringOrNumber", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedValue = GetScalarAsString(fieldElement);
            if (fieldRule.Required && string.IsNullOrWhiteSpace(normalizedValue))
            {
                return ValidationResult.Failure($"השדה '{fieldRule.Field}' לא יכול להיות ריק");
            }

            var patternResult = ValidatePattern(fieldRule, normalizedValue);
            if (!patternResult.IsValid)
            {
                return patternResult;
            }
        }

        if (fieldElement.ValueKind == JsonValueKind.Array)
        {
            var elements = fieldElement.EnumerateArray().ToList();

            if (fieldRule.ArrayLength.HasValue && elements.Count != fieldRule.ArrayLength.Value)
            {
                return ValidationResult.Failure(
                    $"השדה '{fieldRule.Field}' חייב להכיל בדיוק {fieldRule.ArrayLength.Value} פריטים");
            }

            if (fieldRule.MinItems.HasValue && elements.Count < fieldRule.MinItems.Value)
            {
                return ValidationResult.Failure(
                    $"השדה '{fieldRule.Field}' חייב להכיל לפחות {fieldRule.MinItems.Value} פריטים");
            }

            if (fieldRule.MaxItems.HasValue && elements.Count > fieldRule.MaxItems.Value)
            {
                return ValidationResult.Failure(
                    $"השדה '{fieldRule.Field}' לא יכול להכיל יותר מ-{fieldRule.MaxItems.Value} פריטים");
            }

            if (!string.IsNullOrWhiteSpace(fieldRule.ElementType))
            {
                foreach (var element in elements)
                {
                    if (!IsTypeMatch(element, fieldRule.ElementType))
                    {
                        return ValidationResult.Failure(
                            $"כל הפריטים בשדה '{fieldRule.Field}' חייבים להיות מסוג '{fieldRule.ElementType}'");
                    }

                    if (fieldRule.ElementType.Equals("string", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = element.GetString();
                        if (fieldRule.Required && string.IsNullOrWhiteSpace(value))
                        {
                            return ValidationResult.Failure(
                                $"כל הערכים בשדה '{fieldRule.Field}' חייבים להיות מחרוזות לא ריקות");
                        }
                    }
                }
            }
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
                $"השדה '{fieldRule.Field}' אינו שם branch תקין");
        }

        if (patternName.Equals("semantic_version", StringComparison.OrdinalIgnoreCase))
        {
            if (Regex.IsMatch(value, @"^\d+(\.\d+)*$"))
            {
                return ValidationResult.Success();
            }

            return ValidationResult.Failure(
                $"השדה '{fieldRule.Field}' חייב להיות בפורמט גרסה תקין (לדוגמה: 1.0.0)");
        }

        if (Regex.IsMatch(value, patternName))
        {
            return ValidationResult.Success();
        }

        return ValidationResult.Failure(
            $"השדה '{fieldRule.Field}' לא עומד בתבנית הנדרשת");
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
}

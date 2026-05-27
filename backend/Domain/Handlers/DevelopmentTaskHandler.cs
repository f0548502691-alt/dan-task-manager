using System.Text.Json;

namespace DanTaskManager.Domain.Handlers;

/// <summary>
/// מטפל משימות Development
/// סטטוס סופי: 4
/// סטטוס 2: דורש טקסט אפיון (specification)
/// סטטוס 3: דורש שם בראנץ' (branch name)
/// סטטוס 4: דורש מספר גרסה (version number)
/// </summary>
public class DevelopmentTaskHandler : StatusValidationTaskHandlerBase
{
    public DevelopmentTaskHandler()
        : base(new Dictionary<int, Func<string, ValidationResult>>
        {
            [2] = ValidateStatusTwo,
            [3] = ValidateStatusThree,
            [4] = ValidateStatusFour
        })
    {
    }

    public override string TaskType => "Development";

    public override int FinalStatus => 4;

    /// <summary>
    /// וולידציה לסטטוס 2: בדיקת טקסט אפיון
    /// </summary>
    private static ValidationResult ValidateStatusTwo(string newDataJson)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(newDataJson);
            var root = jsonDoc.RootElement;

            // בדיקת קיום שדה "specification"
            if (!root.TryGetProperty("specification", out var specElement))
            {
                return ValidationResult.Failure(
                    "Status 2 requires a 'specification' field containing specification text");
            }

            // בדיקה שזהו מחרוזת
            if (specElement.ValueKind != JsonValueKind.String)
            {
                return ValidationResult.Failure("'specification' must be a string");
            }

            var specification = specElement.GetString();
            if (string.IsNullOrWhiteSpace(specification) || specification.Length < 10)
            {
                return ValidationResult.Failure(
                    "'specification' must be at least 10 characters long");
            }

            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"Invalid JSON payload: {ex.Message}");
        }
    }

    /// <summary>
    /// וולידציה לסטטוס 3: בדיקת שם בראנץ'
    /// </summary>
    private static ValidationResult ValidateStatusThree(string newDataJson)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(newDataJson);
            var root = jsonDoc.RootElement;

            // בדיקת קיום שדה "branchName"
            if (!root.TryGetProperty("branchName", out var branchElement))
            {
                return ValidationResult.Failure(
                    "Status 3 requires a 'branchName' field containing a branch name");
            }

            // בדיקה שזהו מחרוזת
            if (branchElement.ValueKind != JsonValueKind.String)
            {
                return ValidationResult.Failure("'branchName' must be a string");
            }

            var branchName = branchElement.GetString();
            if (string.IsNullOrWhiteSpace(branchName))
            {
                return ValidationResult.Failure("'branchName' cannot be empty");
            }

            // בדיקה בסיסית של כללי Git branch names
            if (branchName.Contains("//") || branchName.EndsWith("/") || 
                branchName.EndsWith(".") || branchName.Contains(" "))
            {
                return ValidationResult.Failure(
                    "Invalid branch name (cannot contain //, end with / or ., or include spaces)");
            }

            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"Invalid JSON payload: {ex.Message}");
        }
    }

    /// <summary>
    /// וולידציה לסטטוס 4: בדיקת מספר גרסה
    /// </summary>
    private static ValidationResult ValidateStatusFour(string newDataJson)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(newDataJson);
            var root = jsonDoc.RootElement;

            // בדיקת קיום שדה "versionNumber"
            if (!root.TryGetProperty("versionNumber", out var versionElement))
            {
                return ValidationResult.Failure(
                    "Status 4 requires a 'versionNumber' field containing a version number");
            }

            // בדיקה שזהו מחרוזת או מספר
            string versionString;
            if (versionElement.ValueKind == JsonValueKind.String)
            {
                versionString = versionElement.GetString() ?? string.Empty;
            }
            else if (versionElement.ValueKind == JsonValueKind.Number)
            {
                versionString = versionElement.GetDouble().ToString();
            }
            else
            {
                return ValidationResult.Failure(
                    "'versionNumber' must be a string or a number");
            }

            if (string.IsNullOrWhiteSpace(versionString))
            {
                return ValidationResult.Failure("'versionNumber' cannot be empty");
            }

            // בדיקה שהוא בפורמט Semantic Versioning (אופציונלי - דוגמה: 1.0.0)
            var parts = versionString.Split('.');
            if (parts.Length >= 2)
            {
                foreach (var part in parts)
                {
                    if (!int.TryParse(part, out _))
                    {
                        return ValidationResult.Failure(
                            $"'versionNumber' must follow SemVer format (for example: 1.0.0), received: {versionString}");
                    }
                }
            }

            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"Invalid JSON payload: {ex.Message}");
        }
    }
}

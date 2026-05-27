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
                    "בסטטוס 2, נדרש שדה 'specification' המכיל טקסט אפיון");
            }

            // בדיקה שזהו מחרוזת
            if (specElement.ValueKind != JsonValueKind.String)
            {
                return ValidationResult.Failure("'specification' חייב להיות מחרוזת");
            }

            var specification = specElement.GetString();
            if (string.IsNullOrWhiteSpace(specification) || specification.Length < 10)
            {
                return ValidationResult.Failure(
                    "'specification' חייב להכיל לפחות 10 תווים");
            }

            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"שגיאה בפענוח JSON: {ex.Message}");
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
                    "בסטטוס 3, נדרש שדה 'branchName' המכיל שם הבראנץ'");
            }

            // בדיקה שזהו מחרוזת
            if (branchElement.ValueKind != JsonValueKind.String)
            {
                return ValidationResult.Failure("'branchName' חייב להיות מחרוזת");
            }

            var branchName = branchElement.GetString();
            if (string.IsNullOrWhiteSpace(branchName))
            {
                return ValidationResult.Failure("'branchName' לא יכול להיות ריק");
            }

            // בדיקה בסיסית של כללי Git branch names
            if (branchName.Contains("//") || branchName.EndsWith("/") || 
                branchName.EndsWith(".") || branchName.Contains(" "))
            {
                return ValidationResult.Failure(
                    "שם הבראנץ' אינו תקין (לא יכול להכיל //, להסתיים ב-/, . או רווחים)");
            }

            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"שגיאה בפענוח JSON: {ex.Message}");
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
                    "בסטטוס 4, נדרש שדה 'versionNumber' המכיל מספר גרסה");
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
                    "'versionNumber' חייב להיות מחרוזת או מספר");
            }

            if (string.IsNullOrWhiteSpace(versionString))
            {
                return ValidationResult.Failure("'versionNumber' לא יכול להיות ריק");
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
                            $"'versionNumber' חייב להיות בפורמט SemVer (לדוגמה: 1.0.0), קיבלנו: {versionString}");
                    }
                }
            }

            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"שגיאה בפענוח JSON: {ex.Message}");
        }
    }
}

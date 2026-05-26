using System.Text.Json;

namespace DanTaskManager.Domain.Handlers;

/// <summary>
/// מטפל משימות Testing
/// סטטוס סופי: 3
/// סטטוס 2: דורש testCases גדול מ-0
/// סטטוס 3: דורש coverage באחוזים ו-summary
/// </summary>
public class TestingTaskHandler : ITaskHandler
{
    public string TaskType => "Testing";

    public int FinalStatus => 3;

    public ValidationResult ValidateStatusChange(
        string currentDataJson,
        int currentStatus,
        int nextStatus,
        string newDataJson)
    {
        if (currentStatus >= FinalStatus && nextStatus > currentStatus)
        {
            return ValidationResult.Failure(
                $"לא ניתן להעביר משימת Testing מעבר לסטטוס {FinalStatus}");
        }

        if (nextStatus == 2)
        {
            return ValidateStatusTwo(newDataJson);
        }

        if (nextStatus == 3)
        {
            return ValidateStatusThree(newDataJson);
        }

        return ValidationResult.Success();
    }

    private static ValidationResult ValidateStatusTwo(string newDataJson)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(newDataJson);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("testCases", out var testCasesElement))
            {
                return ValidationResult.Failure("בסטטוס 2, נדרש שדה 'testCases'");
            }

            if (testCasesElement.ValueKind != JsonValueKind.Number ||
                !testCasesElement.TryGetInt32(out var testCases) ||
                testCases <= 0)
            {
                return ValidationResult.Failure("'testCases' חייב להיות מספר שלם גדול מ-0");
            }

            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"שגיאה בפענוח JSON: {ex.Message}");
        }
    }

    private static ValidationResult ValidateStatusThree(string newDataJson)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(newDataJson);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("coverage", out var coverageElement) ||
                coverageElement.ValueKind != JsonValueKind.String)
            {
                return ValidationResult.Failure("בסטטוס 3, נדרש שדה 'coverage' כמחרוזת אחוזים");
            }

            var coverage = coverageElement.GetString();
            if (string.IsNullOrWhiteSpace(coverage) ||
                !coverage.EndsWith('%') ||
                !int.TryParse(coverage.TrimEnd('%'), out var coveragePercent) ||
                coveragePercent is < 0 or > 100)
            {
                return ValidationResult.Failure("'coverage' חייב להיות בפורמט אחוזים תקין, לדוגמה 85%");
            }

            if (!root.TryGetProperty("summary", out var summaryElement) ||
                summaryElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(summaryElement.GetString()))
            {
                return ValidationResult.Failure("בסטטוס 3, נדרש שדה 'summary' לא ריק");
            }

            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"שגיאה בפענוח JSON: {ex.Message}");
        }
    }
}

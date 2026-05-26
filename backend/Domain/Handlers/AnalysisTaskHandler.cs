using System.Text.Json;

namespace DanTaskManager.Domain.Handlers;

/// <summary>
/// מטפל משימות Analysis
/// סטטוס סופי: 2
/// סטטוס 2: דורש שדה analysisReport
/// </summary>
public class AnalysisTaskHandler : ITaskHandler
{
    public string TaskType => "Analysis";

    public int FinalStatus => 2;

    public ValidationResult ValidateStatusChange(
        string currentDataJson,
        int currentStatus,
        int nextStatus,
        string newDataJson)
    {
        if (currentStatus >= FinalStatus && nextStatus > currentStatus)
        {
            return ValidationResult.Failure(
                $"לא ניתן להעביר משימת Analysis מעבר לסטטוס {FinalStatus}");
        }

        if (nextStatus != 2)
        {
            return ValidationResult.Success();
        }

        try
        {
            using var jsonDoc = JsonDocument.Parse(newDataJson);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("analysisReport", out var reportElement) ||
                reportElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(reportElement.GetString()))
            {
                return ValidationResult.Failure("בסטטוס 2, נדרש שדה 'analysisReport' לא ריק");
            }

            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"שגיאה בפענוח JSON: {ex.Message}");
        }
    }
}

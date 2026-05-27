using System.Text.Json;

namespace DanTaskManager.Domain.Handlers;

/// <summary>
/// מטפל משימות Analysis
/// סטטוס סופי: 2
/// סטטוס 2: דורש שדה analysisReport
/// </summary>
public class AnalysisTaskHandler : IRegisterableTaskHandler
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
                $"Cannot advance Analysis task beyond final status {FinalStatus}");
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
                return ValidationResult.Failure("Status 2 requires a non-empty 'analysisReport' field");
            }

            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"Invalid JSON payload: {ex.Message}");
        }
    }
}

using System.Text.Json;

namespace DanTaskManager.Domain.Handlers;

/// <summary>
/// Testing task handler. Final status = 3.
/// Status 2 requires <c>testCases</c>: integer greater than 0.
/// Status 3 requires <c>coverage</c> as percentage string and a non-empty <c>summary</c>.
/// </summary>
public class TestingTaskHandler : IRegisterableTaskHandler
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
                $"Cannot advance Testing task beyond final status {FinalStatus}");
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
                return ValidationResult.Failure("Status 2 requires a 'testCases' field");
            }

            if (testCasesElement.ValueKind != JsonValueKind.Number ||
                !testCasesElement.TryGetInt32(out var testCases) ||
                testCases <= 0)
            {
                return ValidationResult.Failure("'testCases' must be an integer greater than 0");
            }

            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"Invalid JSON payload: {ex.Message}");
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
                return ValidationResult.Failure("Status 3 requires a 'coverage' field formatted as a percentage string");
            }

            var coverage = coverageElement.GetString();
            if (string.IsNullOrWhiteSpace(coverage) ||
                !coverage.EndsWith('%') ||
                !int.TryParse(coverage.TrimEnd('%'), out var coveragePercent) ||
                coveragePercent is < 0 or > 100)
            {
                return ValidationResult.Failure("'coverage' must be a valid percentage format, for example 85%");
            }

            if (!root.TryGetProperty("summary", out var summaryElement) ||
                summaryElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(summaryElement.GetString()))
            {
                return ValidationResult.Failure("Status 3 requires a non-empty 'summary' field");
            }

            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"Invalid JSON payload: {ex.Message}");
        }
    }
}

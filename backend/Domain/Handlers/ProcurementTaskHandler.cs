using System.Text.Json;

namespace DanTaskManager.Domain.Handlers;

/// <summary>
/// Procurement task handler. Final status = 3.
/// Status 2 requires <c>prices</c>: array of exactly 2 non-empty strings.
/// Status 3 requires <c>receipt</c>: non-empty string.
/// </summary>
public class ProcurementTaskHandler : StatusValidationTaskHandlerBase
{
    public ProcurementTaskHandler()
        : base(new Dictionary<int, Func<string, ValidationResult>>
        {
            [2] = ValidateStatusTwo,
            [3] = ValidateStatusThree
        })
    {
    }

    public override string TaskType => "Procurement";

    public override int FinalStatus => 3;

    private static ValidationResult ValidateStatusTwo(string newDataJson)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(newDataJson);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("prices", out var pricesElement))
            {
                return ValidationResult.Failure(
                    "Status 2 requires a 'prices' field containing an array of two quote strings");
            }

            if (pricesElement.ValueKind != JsonValueKind.Array)
            {
                return ValidationResult.Failure("'prices' must be an array");
            }

            var prices = pricesElement.EnumerateArray().ToList();
            if (prices.Count != 2)
            {
                return ValidationResult.Failure(
                    $"'prices' must contain exactly 2 strings, found {prices.Count}");
            }

            foreach (var price in prices)
            {
                if (price.ValueKind != JsonValueKind.String)
                {
                    return ValidationResult.Failure("All prices must be strings");
                }

                var priceString = price.GetString();
                if (string.IsNullOrWhiteSpace(priceString))
                {
                    return ValidationResult.Failure("Price values cannot be empty");
                }
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

            if (!root.TryGetProperty("receipt", out var receiptElement))
            {
                return ValidationResult.Failure(
                    "Status 3 requires a 'receipt' field containing a receipt string");
            }

            if (receiptElement.ValueKind != JsonValueKind.String)
            {
                return ValidationResult.Failure("'receipt' must be a string");
            }

            var receipt = receiptElement.GetString();
            if (string.IsNullOrWhiteSpace(receipt))
            {
                return ValidationResult.Failure("'receipt' cannot be empty");
            }

            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"Invalid JSON payload: {ex.Message}");
        }
    }
}

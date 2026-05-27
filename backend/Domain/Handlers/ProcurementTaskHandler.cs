using System.Text.Json;

namespace DanTaskManager.Domain.Handlers;

/// <summary>
/// מטפל משימות Procurement
/// סטטוס סופי: 3
/// סטטוס 2: דורש מערך של 2 מחרוזות (מחירים)
/// סטטוס 3: דורש קבלה (מחרוזת)
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

    /// <summary>
    /// וולידציה לסטטוס 2: בדיקת מערך מחירים (2 מחרוזות)
    /// </summary>
    private static ValidationResult ValidateStatusTwo(string newDataJson)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(newDataJson);
            var root = jsonDoc.RootElement;

            // בדיקת קיום שדה "prices"
            if (!root.TryGetProperty("prices", out var pricesElement))
            {
                return ValidationResult.Failure(
                    "Status 2 requires a 'prices' field containing an array of two quote strings");
            }

            // בדיקה שזהו מערך
            if (pricesElement.ValueKind != JsonValueKind.Array)
            {
                return ValidationResult.Failure("'prices' must be an array");
            }

            // בדיקה של בדיוק 2 מחרוזות
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

    /// <summary>
    /// וולידציה לסטטוס 3: בדיקת קבלה (מחרוזת)
    /// </summary>
    private static ValidationResult ValidateStatusThree(string newDataJson)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(newDataJson);
            var root = jsonDoc.RootElement;

            // בדיקת קיום שדה "receipt"
            if (!root.TryGetProperty("receipt", out var receiptElement))
            {
                return ValidationResult.Failure(
                    "Status 3 requires a 'receipt' field containing a receipt string");
            }

            // בדיקה שזהו מחרוזת
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

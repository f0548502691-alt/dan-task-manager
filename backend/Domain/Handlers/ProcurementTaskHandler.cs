using System.Text.Json;

namespace DanTaskManager.Domain.Handlers;

/// <summary>
/// מטפל משימות Procurement
/// סטטוס סופי: 3
/// סטטוס 2: דורש מערך של 2 מחרוזות (מחירים)
/// סטטוס 3: דורש קבלה (מחרוזת)
/// </summary>
public class ProcurementTaskHandler : ITaskHandler
{
    public string TaskType => "Procurement";

    public int FinalStatus => 3;

    public ValidationResult ValidateStatusChange(
        string currentDataJson,
        int currentStatus,
        int nextStatus,
        string newDataJson)
    {
        // לא יכול להעבור את הסטטוס הסופי
        if (currentStatus >= FinalStatus && nextStatus > currentStatus)
        {
            return ValidationResult.Failure(
                $"לא ניתן להעביר משימת Procurement מעבר לסטטוס {FinalStatus}");
        }

        // וולידציה ספציפית לסטטוס 2
        if (nextStatus == 2)
        {
            return ValidateStatusTwo(newDataJson);
        }

        // וולידציה ספציפית לסטטוס 3
        if (nextStatus == 3)
        {
            return ValidateStatusThree(newDataJson);
        }

        // סטטוסים אחרים לא דורשים וולידציה
        return ValidationResult.Success();
    }

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
                    "בסטטוס 2, נדרש שדה 'prices' המכיל מערך של 2 מחרוזות (מחירים)");
            }

            // בדיקה שזהו מערך
            if (pricesElement.ValueKind != JsonValueKind.Array)
            {
                return ValidationResult.Failure("'prices' חייב להיות מערך");
            }

            // בדיקה של בדיוק 2 מחרוזות
            var prices = pricesElement.EnumerateArray().ToList();
            if (prices.Count != 2)
            {
                return ValidationResult.Failure(
                    $"'prices' חייב להכיל בדיוק 2 מחרוזות, נמצאו {prices.Count}");
            }

            foreach (var price in prices)
            {
                if (price.ValueKind != JsonValueKind.String)
                {
                    return ValidationResult.Failure("כל המחירים חייבים להיות מחרוזות");
                }

                var priceString = price.GetString();
                if (string.IsNullOrWhiteSpace(priceString))
                {
                    return ValidationResult.Failure("המחירים לא יכולים להיות ריקים");
                }
            }

            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"שגיאה בפענוח JSON: {ex.Message}");
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
                    "בסטטוס 3, נדרש שדה 'receipt' המכיל מחרוזת של קבלה");
            }

            // בדיקה שזהו מחרוזת
            if (receiptElement.ValueKind != JsonValueKind.String)
            {
                return ValidationResult.Failure("'receipt' חייב להיות מחרוזת");
            }

            var receipt = receiptElement.GetString();
            if (string.IsNullOrWhiteSpace(receipt))
            {
                return ValidationResult.Failure("'receipt' לא יכול להיות ריק");
            }

            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Failure($"שגיאה בפענוח JSON: {ex.Message}");
        }
    }
}

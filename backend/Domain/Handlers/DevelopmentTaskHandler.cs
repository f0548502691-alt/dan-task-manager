using System.Text.Json;

namespace DanTaskManager.Domain.Handlers;

/// <summary>
/// Development task handler. Final status = 4.
/// Status 2 requires <c>specification</c>: string of at least 10 characters.
/// Status 3 requires <c>branchName</c>: valid Git branch name.
/// Status 4 requires <c>versionNumber</c>: string or number in SemVer format.
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

    private static ValidationResult ValidateStatusTwo(string newDataJson)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(newDataJson);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("specification", out var specElement))
            {
                return ValidationResult.Failure(
                    "Status 2 requires a 'specification' field containing specification text");
            }

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

    private static ValidationResult ValidateStatusThree(string newDataJson)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(newDataJson);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("branchName", out var branchElement))
            {
                return ValidationResult.Failure(
                    "Status 3 requires a 'branchName' field containing a branch name");
            }

            if (branchElement.ValueKind != JsonValueKind.String)
            {
                return ValidationResult.Failure("'branchName' must be a string");
            }

            var branchName = branchElement.GetString();
            if (string.IsNullOrWhiteSpace(branchName))
            {
                return ValidationResult.Failure("'branchName' cannot be empty");
            }

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

    private static ValidationResult ValidateStatusFour(string newDataJson)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(newDataJson);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("versionNumber", out var versionElement))
            {
                return ValidationResult.Failure(
                    "Status 4 requires a 'versionNumber' field containing a version number");
            }

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

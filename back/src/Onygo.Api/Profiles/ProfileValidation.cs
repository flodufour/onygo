namespace Onygo.Api.Profiles;

/// <summary>
/// Renvoie les erreurs par champ, au format attendu par ValidationProblem (clés en camelCase comme le JSON).
/// </summary>
public static class ProfileValidation
{
    private const string FirstNameField = "firstName";
    private const string BirthDateField = "birthDate";

    public static Dictionary<string, string[]> ValidateCreation(CreateProfileRequest request, DateOnly today)
    {
        var errors = new Dictionary<string, string[]>();
        AddFirstNameError(errors, request.FirstName);
        AddBirthDateError(errors, request.BirthDate, today);
        return errors;
    }

    public static Dictionary<string, string[]> ValidateUpdate(UpdateProfileRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.FirstName is not null)
        {
            AddFirstNameError(errors, request.FirstName);
        }

        if (request.BirthDate is not null)
        {
            errors[BirthDateField] = [ProfileErrorCodes.BirthDateNotEditable];
        }

        return errors;
    }

    private static void AddFirstNameError(Dictionary<string, string[]> errors, string? firstName)
    {
        var trimmed = firstName?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            errors[FirstNameField] = [ProfileErrorCodes.FirstNameRequired];
        }
        else if (trimmed.Length > ProfileRules.MaxFirstNameLength)
        {
            errors[FirstNameField] = [ProfileErrorCodes.FirstNameTooLong];
        }
    }

    private static void AddBirthDateError(Dictionary<string, string[]> errors, DateOnly? birthDate, DateOnly today)
    {
        if (birthDate is null)
        {
            errors[BirthDateField] = [ProfileErrorCodes.BirthDateRequired];
        }
        else if (!ProfileRules.IsAdult(birthDate.Value, today))
        {
            errors[BirthDateField] = [ProfileErrorCodes.Underage];
        }
    }
}

namespace Onygo.Api.Profiles;

public sealed record CreateProfileRequest(string? FirstName, DateOnly? BirthDate);

// BirthDate n'est accepté que pour être refusé explicitement : la date de naissance n'est pas modifiable.
public sealed record UpdateProfileRequest(string? FirstName, DateOnly? BirthDate);

public sealed record ProfileResponse(string FirstName, DateOnly BirthDate, int Age);

/// <summary>
/// Codes d'erreur renvoyés par l'API, traduits côté front.
/// </summary>
public static class ProfileErrorCodes
{
    public const string FirstNameRequired = "FirstNameRequired";
    public const string FirstNameTooLong = "FirstNameTooLong";
    public const string BirthDateRequired = "BirthDateRequired";
    public const string Underage = "Underage";
    public const string BirthDateNotEditable = "BirthDateNotEditable";
}

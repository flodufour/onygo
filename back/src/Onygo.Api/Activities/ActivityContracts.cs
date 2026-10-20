namespace Onygo.Api.Activities;

public sealed record CreateActivityRequest(
    ActivityType? Type,
    string? CustomType,
    string? Title,
    string? Hook,
    ActivityLocationRequest? Location,
    DateTimeOffset? StartsAt);

public sealed record ActivityLocationRequest(string? Address, double? Latitude, double? Longitude);

public sealed record ActivityResponse(
    Guid Id,
    ActivityType Type,
    string? CustomType,
    string Title,
    string Hook,
    DateTimeOffset StartsAt,
    ActivityStatus Status,
    PublicAreaResponse PublicArea,
    ExactLocationResponse? ExactLocation,
    OrganizerResponse Organizer,
    bool ShortNoticeWarning);

/// <summary>Une page du feed ; NextCursor est null quand il n'y a plus rien après.</summary>
public sealed record FeedPageResponse(IReadOnlyList<ActivityResponse> Activities, string? NextCursor);

/// <summary>Zone approximative visible par tous : le lieu exact est quelque part dans ce cercle.</summary>
public sealed record PublicAreaResponse(double Latitude, double Longitude, int RadiusMeters);

/// <summary>Lieu exact, null pour qui n'y a pas droit.</summary>
public sealed record ExactLocationResponse(string Address, double Latitude, double Longitude);

public sealed record OrganizerResponse(string FirstName, int Age);

/// <summary>
/// Codes d'erreur renvoyés par l'API, traduits côté front.
/// </summary>
public static class ActivityErrorCodes
{
    public const string ProfileRequired = "ProfileRequired";
    public const string TypeRequired = "TypeRequired";
    public const string CustomTypeRequired = "CustomTypeRequired";
    public const string CustomTypeTooLong = "CustomTypeTooLong";
    public const string CustomTypeNotAllowed = "CustomTypeNotAllowed";
    public const string TitleRequired = "TitleRequired";
    public const string TitleTooLong = "TitleTooLong";
    public const string HookRequired = "HookRequired";
    public const string HookTooLong = "HookTooLong";
    public const string LocationRequired = "LocationRequired";
    public const string AddressRequired = "AddressRequired";
    public const string AddressTooLong = "AddressTooLong";
    public const string LatitudeInvalid = "LatitudeInvalid";
    public const string LongitudeInvalid = "LongitudeInvalid";
    public const string StartsAtRequired = "StartsAtRequired";
    public const string StartsAtInPast = "StartsAtInPast";
    public const string StartsAtTooFarAhead = "StartsAtTooFarAhead";
    public const string InvalidCursor = "InvalidCursor";
    public const string LimitOutOfRange = "LimitOutOfRange";
}

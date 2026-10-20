namespace Onygo.Api.Applications;

public sealed record ApplyRequest(string? Message);

/// <summary>
/// Statut vu par le candidat. Volontairement sans Refused ni Expired : un refus reste silencieux (R9).
/// </summary>
public enum MyApplicationStatus
{
    Pending,
    Accepted,
}

public sealed record MyApplicationResponse(
    Guid Id,
    Guid ActivityId,
    string ActivityTitle,
    DateTimeOffset ActivityStartsAt,
    MyApplicationStatus Status,
    string? Message,
    DateTimeOffset AppliedAt,
    bool ShortNoticeWarning);

/// <summary>Candidature vue par l'organisateur quand il trie (O / X).</summary>
public sealed record ReceivedApplicationResponse(
    Guid Id,
    string ApplicantFirstName,
    int ApplicantAge,
    string? Message,
    DateTimeOffset AppliedAt);

public sealed record AcceptedApplicationResponse(Guid Id, bool ShortNoticeWarning);

/// <summary>
/// Codes d'erreur renvoyés par l'API (champ « code » du ProblemDetails), traduits côté front.
/// </summary>
public static class ApplicationErrorCodes
{
    public const string ProfileRequired = "ProfileRequired";
    public const string MessageTooLong = "MessageTooLong";
    public const string CannotApplyToOwnActivity = "CannotApplyToOwnActivity";
    public const string ActivityNotOpen = "ActivityNotOpen";
    public const string ActivityAlreadyStarted = "ActivityAlreadyStarted";
    public const string AlreadyApplied = "AlreadyApplied";
    public const string ActivityFull = "ActivityFull";
    public const string DailyApplicationLimitReached = "DailyApplicationLimitReached";
    public const string NotOrganizer = "NotOrganizer";
    public const string ApplicationNotPending = "ApplicationNotPending";
}

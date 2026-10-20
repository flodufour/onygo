using Onygo.Api.Activities;

namespace Onygo.Api.Applications;

/// <summary>Ce qu'il faut savoir pour décider si une candidature est possible (lu sous verrou).</summary>
public sealed record ApplyCheck(
    Activity Activity,
    string ApplicantId,
    DateTimeOffset Now,
    bool HasAlreadyApplied,
    int PendingApplicationsCount,
    int ApplicationsTodayCount);

public enum Decision
{
    Accept,
    Refuse,
}

public sealed record DecisionCheck(
    Activity Activity,
    ActivityApplication Application,
    string DeciderId,
    DateTimeOffset Now,
    Decision Decision);

/// <summary>
/// Règles métier des candidatures (CLAUDE.md racine §2.4). Renvoient le code d'erreur de la première règle violée, ou null.
/// </summary>
public static class ApplicationRules
{
    public const int MaxPendingApplicationsPerActivity = 3;
    public const int MaxApplicationsPerDay = 5;
    public const int MaxMessageLength = 200;

    public static string? CheckCanApply(ApplyCheck check)
    {
        var activity = check.Activity;
        if (activity.OrganizerId == check.ApplicantId)
        {
            return ApplicationErrorCodes.CannotApplyToOwnActivity;
        }

        if (activity.Status != ActivityStatus.Open)
        {
            return ApplicationErrorCodes.ActivityNotOpen;
        }

        if (activity.StartsAt <= check.Now)
        {
            return ApplicationErrorCodes.ActivityAlreadyStarted;
        }

        if (check.HasAlreadyApplied)
        {
            return ApplicationErrorCodes.AlreadyApplied;
        }

        if (check.PendingApplicationsCount >= MaxPendingApplicationsPerActivity)
        {
            return ApplicationErrorCodes.ActivityFull;
        }

        if (check.ApplicationsTodayCount >= MaxApplicationsPerDay)
        {
            return ApplicationErrorCodes.DailyApplicationLimitReached;
        }

        return null;
    }

    public static string? CheckCanDecide(DecisionCheck check)
    {
        if (check.Activity.OrganizerId != check.DeciderId)
        {
            return ApplicationErrorCodes.NotOrganizer;
        }

        if (check.Application.Status != ApplicationStatus.Pending)
        {
            return ApplicationErrorCodes.ApplicationNotPending;
        }

        if (check.Decision == Decision.Refuse)
        {
            return null;
        }

        if (check.Activity.Status != ActivityStatus.Open)
        {
            return ApplicationErrorCodes.ActivityNotOpen;
        }

        return check.Activity.StartsAt <= check.Now ? ApplicationErrorCodes.ActivityAlreadyStarted : null;
    }
}

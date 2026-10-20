using Onygo.Api.Activities;
using Onygo.Api.Applications;
using Onygo.Api.Common;

namespace Onygo.Api.UnitTests.Applications;

public class ApplicationRulesTests
{
    private const string OrganizerId = "organizer";
    private const string ApplicantId = "applicant";
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 8, 0, 0, TimeSpan.Zero);

    private static Activity AnOpenActivity(DateTimeOffset? startsAt = null) => new()
    {
        OrganizerId = OrganizerId,
        Type = ActivityType.Cinema,
        Title = "Ciné au Katorza",
        Hook = "Le dernier Miyazaki ?",
        Location = new ActivityLocation("Katorza, Nantes", 47.2135, -1.5590),
        PublicAreaCenter = new GeoPoint(47.22, -1.55),
        StartsAt = startsAt ?? Now.AddDays(1),
        CreatedAt = Now.AddDays(-1),
    };

    private static ApplyCheck AValidApplyCheck() => new(
        Activity: AnOpenActivity(),
        ApplicantId: ApplicantId,
        Now: Now,
        HasAlreadyApplied: false,
        PendingApplicationsCount: 0,
        ApplicationsTodayCount: 0);

    private static ActivityApplication APendingApplication() => new()
    {
        ActivityId = Guid.NewGuid(),
        ApplicantId = ApplicantId,
        AppliedAt = Now.AddHours(-1),
    };

    private static DecisionCheck AnAcceptanceByTheOrganizer() =>
        new(AnOpenActivity(), APendingApplication(), OrganizerId, Now, Decision.Accept);

    [Fact]
    public void CheckCanApply_WhenEverythingIsFine_ReturnsNull()
    {
        var error = ApplicationRules.CheckCanApply(AValidApplyCheck());

        Assert.Null(error);
    }

    [Fact]
    public void CheckCanApply_ToOwnActivity_ReturnsCannotApplyToOwnActivity()
    {
        var error = ApplicationRules.CheckCanApply(AValidApplyCheck() with { ApplicantId = OrganizerId });

        Assert.Equal(ApplicationErrorCodes.CannotApplyToOwnActivity, error);
    }

    [Fact]
    public void CheckCanApply_ToFilledActivity_ReturnsActivityNotOpen()
    {
        var activity = AnOpenActivity();
        activity.MarkFilled();

        var error = ApplicationRules.CheckCanApply(AValidApplyCheck() with { Activity = activity });

        Assert.Equal(ApplicationErrorCodes.ActivityNotOpen, error);
    }

    // R5 : les candidatures ferment 1 h avant le début.
    [Fact]
    public void CheckCanApply_Exactly1HourBeforeStart_ReturnsApplicationsClosed()
    {
        var activity = AnOpenActivity(startsAt: Now.AddHours(1));

        var error = ApplicationRules.CheckCanApply(AValidApplyCheck() with { Activity = activity });

        Assert.Equal(ApplicationErrorCodes.ApplicationsClosed, error);
    }

    [Fact]
    public void CheckCanApply_1HourAnd1MinuteBeforeStart_ReturnsNull()
    {
        var activity = AnOpenActivity(startsAt: Now.AddHours(1).AddMinutes(1));

        var error = ApplicationRules.CheckCanApply(AValidApplyCheck() with { Activity = activity });

        Assert.Null(error);
    }

    [Fact]
    public void CheckCanApply_WhenAlreadyApplied_ReturnsAlreadyApplied()
    {
        var error = ApplicationRules.CheckCanApply(AValidApplyCheck() with { HasAlreadyApplied = true });

        Assert.Equal(ApplicationErrorCodes.AlreadyApplied, error);
    }

    [Fact]
    public void CheckCanApply_WhenActivityHas3PendingApplications_ReturnsActivityFull()
    {
        var error = ApplicationRules.CheckCanApply(AValidApplyCheck() with { PendingApplicationsCount = 3 });

        Assert.Equal(ApplicationErrorCodes.ActivityFull, error);
    }

    [Fact]
    public void CheckCanApply_WhenActivityHas2PendingApplications_ReturnsNull()
    {
        var error = ApplicationRules.CheckCanApply(AValidApplyCheck() with { PendingApplicationsCount = 2 });

        Assert.Null(error);
    }

    [Fact]
    public void CheckCanApply_After5ApplicationsToday_ReturnsDailyApplicationLimitReached()
    {
        var error = ApplicationRules.CheckCanApply(AValidApplyCheck() with { ApplicationsTodayCount = 5 });

        Assert.Equal(ApplicationErrorCodes.DailyApplicationLimitReached, error);
    }

    [Fact]
    public void CheckCanApply_After4ApplicationsToday_ReturnsNull()
    {
        var error = ApplicationRules.CheckCanApply(AValidApplyCheck() with { ApplicationsTodayCount = 4 });

        Assert.Null(error);
    }

    [Fact]
    public void CheckCanDecide_AcceptanceByTheOrganizer_ReturnsNull()
    {
        var error = ApplicationRules.CheckCanDecide(AnAcceptanceByTheOrganizer());

        Assert.Null(error);
    }

    [Fact]
    public void CheckCanDecide_BySomeoneElse_ReturnsNotOrganizer()
    {
        var error = ApplicationRules.CheckCanDecide(AnAcceptanceByTheOrganizer() with { DeciderId = ApplicantId });

        Assert.Equal(ApplicationErrorCodes.NotOrganizer, error);
    }

    [Fact]
    public void CheckCanDecide_OnRefusedApplication_ReturnsApplicationNotPending()
    {
        var application = APendingApplication();
        application.Refuse(Now);

        var error = ApplicationRules.CheckCanDecide(AnAcceptanceByTheOrganizer() with { Application = application });

        Assert.Equal(ApplicationErrorCodes.ApplicationNotPending, error);
    }

    // R5 : une candidature acceptée moins d'1 h avant le début aurait déjà dû expirer.
    [Fact]
    public void CheckCanDecide_AcceptanceExactly1HourBeforeStart_ReturnsApplicationsClosed()
    {
        var check = AnAcceptanceByTheOrganizer() with { Now = Now.AddDays(1).AddHours(-1) };

        var error = ApplicationRules.CheckCanDecide(check);

        Assert.Equal(ApplicationErrorCodes.ApplicationsClosed, error);
    }

    [Fact]
    public void CheckCanDecide_RefusalAfterActivityStarted_ReturnsNull()
    {
        var check = AnAcceptanceByTheOrganizer() with { Now = Now.AddDays(2), Decision = Decision.Refuse };

        var error = ApplicationRules.CheckCanDecide(check);

        Assert.Null(error);
    }
}

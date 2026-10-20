using Microsoft.EntityFrameworkCore;
using Onygo.Api.Activities;
using Onygo.Api.Applications;
using Onygo.Api.IntegrationTests.Infrastructure;
using static Onygo.Api.IntegrationTests.Activities.ActivityRequests;

namespace Onygo.Api.IntegrationTests.Applications;

/// <summary>
/// R5 : 2 h avant le début, les candidatures en attente et les activités encore ouvertes expirent.
/// </summary>
[Collection(ApiCollection.Name)]
public class ExpirationTests(OnygoApiFactory factory)
{
    private readonly AuthApiClient _auth = new(factory);

    [Fact]
    public async Task Expiration_PendingApplication2HoursBeforeStart_BecomesExpired()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var applicationId = await applicant.ApplySuccessfullyAsync(activity.Id);
        await factory.SetActivityStartAsync(activity.Id, OnygoApiFactory.Now.AddHours(2));

        await factory.RunExpirationAsync();

        Assert.Equal(ApplicationStatus.Expired, await ApplicationStatusAsync(applicationId));
    }

    [Fact]
    public async Task Expiration_PendingApplication2HoursAnd1MinuteBeforeStart_StaysPending()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(
            AValidActivity() with { StartsAt = OnygoApiFactory.Now.AddHours(2).AddMinutes(1) });
        var applicationId = await applicant.ApplySuccessfullyAsync(activity.Id);

        await factory.RunExpirationAsync();

        Assert.Equal(ApplicationStatus.Pending, await ApplicationStatusAsync(applicationId));
    }

    [Fact]
    public async Task Expiration_OpenActivityWithoutAcceptance_BecomesExpired()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        await factory.SetActivityStartAsync(activity.Id, OnygoApiFactory.Now.AddHours(1));

        await factory.RunExpirationAsync();

        Assert.Equal(ActivityStatus.Expired, (await organizer.GetActivityAsync(activity.Id)).Status);
    }

    [Fact]
    public async Task Expiration_KeepsFilledActivityAndAcceptedApplication()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var applicationId = await applicant.ApplySuccessfullyAsync(activity.Id);
        await organizer.AcceptAsync(applicationId);
        await factory.SetActivityStartAsync(activity.Id, OnygoApiFactory.Now.AddHours(1));

        await factory.RunExpirationAsync();

        Assert.Equal(ActivityStatus.Filled, (await organizer.GetActivityAsync(activity.Id)).Status);
        Assert.Equal(ApplicationStatus.Accepted, await ApplicationStatusAsync(applicationId));
    }

    // R9 : une candidature expirée disparaît de « Mes candidatures », sans explication.
    [Fact]
    public async Task Expiration_ExpiredApplication_DisappearsFromApplicantList()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        await applicant.ApplySuccessfullyAsync(activity.Id);
        await factory.SetActivityStartAsync(activity.Id, OnygoApiFactory.Now.AddHours(1));

        await factory.RunExpirationAsync();

        Assert.Empty(await applicant.GetMyApplicationsAsync());
    }

    [Fact]
    public async Task Expiration_RunTwice_ChangesNothingTheSecondTime()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        await applicant.ApplySuccessfullyAsync(activity.Id);
        await factory.SetActivityStartAsync(activity.Id, OnygoApiFactory.Now.AddHours(1));
        await factory.RunExpirationAsync();

        var secondRun = await factory.RunExpirationAsync();

        Assert.Equal(new ExpirationResult(ExpiredActivities: 0, ExpiredApplications: 0), secondRun);
    }

    private Task<ApplicationStatus> ApplicationStatusAsync(Guid applicationId) =>
        factory.WithDatabaseAsync(db =>
            db.Applications.Where(a => a.Id == applicationId).Select(a => a.Status).SingleAsync());
}

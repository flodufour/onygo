using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Onygo.Api.Applications;
using Onygo.Api.IntegrationTests.Infrastructure;
using static Onygo.Api.IntegrationTests.Activities.ActivityRequests;

namespace Onygo.Api.IntegrationTests.Applications;

[Collection(ApiCollection.Name)]
public class ApplyTests(OnygoApiFactory factory)
{
    private readonly AuthApiClient _auth = new(factory);

    [Fact]
    public async Task Apply_ToOpenActivity_ReturnsPendingApplication()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());

        var response = await applicant.ApplyAsync(activity.Id, message: "Avec plaisir !");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var application = await response.Content.ReadFromJsonAsync<MyApplicationResponse>(TestJson.Options);
        Assert.Equal(MyApplicationStatus.Pending, application!.Status);
        Assert.Equal("Avec plaisir !", application.Message);
        Assert.False(application.ShortNoticeWarning);
    }

    [Fact]
    public async Task Apply_ToActivityStartingInLessThan6Hours_ReturnsShortNoticeWarning()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity() with { StartsAt = OnygoApiFactory.Now.AddHours(5) });

        var response = await applicant.ApplyAsync(activity.Id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var application = await response.Content.ReadFromJsonAsync<MyApplicationResponse>(TestJson.Options);
        Assert.True(application!.ShortNoticeWarning);
    }

    [Fact]
    public async Task Apply_WithoutProfile_ReturnsProfileRequired()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicantWithoutProfile = await _auth.CreateSignedInClientAsync();
        var activity = await organizer.CreateActivityAsync(AValidActivity());

        var response = await applicantWithoutProfile.ApplyAsync(activity.Id);

        await response.AssertBusinessErrorAsync(HttpStatusCode.Forbidden, ApplicationErrorCodes.ProfileRequired);
    }

    [Fact]
    public async Task Apply_WithTooLongMessage_ReturnsBadRequest()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());

        var response = await applicant.ApplyAsync(activity.Id, new string('a', ApplicationRules.MaxMessageLength + 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Apply_ToUnknownActivity_ReturnsNotFound()
    {
        var applicant = await _auth.CreateSignedInClientWithProfileAsync();

        var response = await applicant.ApplyAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // R7
    [Fact]
    public async Task Apply_ToOwnActivity_ReturnsCannotApplyToOwnActivity()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var activity = await organizer.CreateActivityAsync(AValidActivity());

        var response = await organizer.ApplyAsync(activity.Id);

        await response.AssertBusinessErrorAsync(HttpStatusCode.Forbidden, ApplicationErrorCodes.CannotApplyToOwnActivity);
    }

    // R3
    [Fact]
    public async Task Apply_Twice_ReturnsAlreadyApplied()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        await applicant.ApplySuccessfullyAsync(activity.Id);

        var response = await applicant.ApplyAsync(activity.Id);

        await response.AssertBusinessErrorAsync(HttpStatusCode.Conflict, ApplicationErrorCodes.AlreadyApplied);
    }

    // R3
    [Fact]
    public async Task Apply_AfterBeingRefused_ReturnsAlreadyApplied()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var applicationId = await applicant.ApplySuccessfullyAsync(activity.Id);
        await organizer.RefuseAsync(applicationId);

        var response = await applicant.ApplyAsync(activity.Id);

        await response.AssertBusinessErrorAsync(HttpStatusCode.Conflict, ApplicationErrorCodes.AlreadyApplied);
    }

    // R1
    [Fact]
    public async Task Apply_WhenActivityHas3PendingApplications_ReturnsActivityFull()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        await (await _auth.CreateSignedInClientWithProfileAsync()).ApplySuccessfullyAsync(activity.Id);
        await (await _auth.CreateSignedInClientWithProfileAsync()).ApplySuccessfullyAsync(activity.Id);
        await (await _auth.CreateSignedInClientWithProfileAsync()).ApplySuccessfullyAsync(activity.Id);
        var fourthApplicant = await _auth.CreateSignedInClientWithProfileAsync();

        var response = await fourthApplicant.ApplyAsync(activity.Id);

        await response.AssertBusinessErrorAsync(HttpStatusCode.Conflict, ApplicationErrorCodes.ActivityFull);
    }

    // R2
    [Fact]
    public async Task Apply_AfterARefusalFreedASpot_ReturnsCreated()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var refusedApplicationId = await (await _auth.CreateSignedInClientWithProfileAsync()).ApplySuccessfullyAsync(activity.Id);
        await (await _auth.CreateSignedInClientWithProfileAsync()).ApplySuccessfullyAsync(activity.Id);
        await (await _auth.CreateSignedInClientWithProfileAsync()).ApplySuccessfullyAsync(activity.Id);
        await organizer.RefuseAsync(refusedApplicationId);
        var fourthApplicant = await _auth.CreateSignedInClientWithProfileAsync();

        var response = await fourthApplicant.ApplyAsync(activity.Id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // R8
    [Fact]
    public async Task Apply_ToFilledActivity_ReturnsActivityNotOpen()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var acceptedApplicationId = await (await _auth.CreateSignedInClientWithProfileAsync()).ApplySuccessfullyAsync(activity.Id);
        await organizer.AcceptAsync(acceptedApplicationId);
        var lateApplicant = await _auth.CreateSignedInClientWithProfileAsync();

        var response = await lateApplicant.ApplyAsync(activity.Id);

        await response.AssertBusinessErrorAsync(HttpStatusCode.Conflict, ApplicationErrorCodes.ActivityNotOpen);
    }

    // R5 / R8 : les candidatures ferment 1 h avant le début.
    [Fact]
    public async Task Apply_1HourBeforeStart_ReturnsApplicationsClosed()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        await factory.SetActivityStartAsync(activity.Id, OnygoApiFactory.Now.AddHours(1));

        var response = await applicant.ApplyAsync(activity.Id);

        await response.AssertBusinessErrorAsync(HttpStatusCode.Conflict, ApplicationErrorCodes.ApplicationsClosed);
    }

    // R6
    [Fact]
    public async Task Apply_SixthApplicationOfTheDay_ReturnsDailyApplicationLimitReached()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activities = await organizer.CreateActivitiesAsync(AValidActivity(), count: 6);
        await applicant.ApplySuccessfullyAsync(activities[0].Id);
        await applicant.ApplySuccessfullyAsync(activities[1].Id);
        await applicant.ApplySuccessfullyAsync(activities[2].Id);
        await applicant.ApplySuccessfullyAsync(activities[3].Id);
        await applicant.ApplySuccessfullyAsync(activities[4].Id);

        var response = await applicant.ApplyAsync(activities[5].Id);

        await response.AssertBusinessErrorAsync(HttpStatusCode.Conflict, ApplicationErrorCodes.DailyApplicationLimitReached);
    }

    // R6 : la limite se compte par jour calendaire à Paris.
    [Fact]
    public async Task Apply_AfterFiveApplicationsYesterday_ReturnsCreated()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activities = await organizer.CreateActivitiesAsync(AValidActivity(), count: 6);
        var yesterdayApplicationIds = new[]
        {
            await applicant.ApplySuccessfullyAsync(activities[0].Id),
            await applicant.ApplySuccessfullyAsync(activities[1].Id),
            await applicant.ApplySuccessfullyAsync(activities[2].Id),
            await applicant.ApplySuccessfullyAsync(activities[3].Id),
            await applicant.ApplySuccessfullyAsync(activities[4].Id),
        };
        // 23 h 59 la veille, heure de Paris.
        var lastMinuteOfYesterday = new DateTimeOffset(2026, 6, 14, 23, 59, 0, TimeSpan.FromHours(2)).ToUniversalTime();
        await factory.WithDatabaseAsync(db => db.Applications
            .Where(a => yesterdayApplicationIds.Contains(a.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.AppliedAt, lastMinuteOfYesterday)));

        var response = await applicant.ApplyAsync(activities[5].Id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}

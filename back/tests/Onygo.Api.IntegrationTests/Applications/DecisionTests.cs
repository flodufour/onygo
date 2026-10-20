using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Onygo.Api.Activities;
using Onygo.Api.Applications;
using Onygo.Api.IntegrationTests.Infrastructure;
using static Onygo.Api.IntegrationTests.Activities.ActivityRequests;

namespace Onygo.Api.IntegrationTests.Applications;

[Collection(ApiCollection.Name)]
public class DecisionTests(OnygoApiFactory factory)
{
    private readonly AuthApiClient _auth = new(factory);

    [Fact]
    public async Task ReceivedApplications_AsOrganizer_ListsPendingApplicantsWithFirstNameAgeAndMessage()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa", age: 22);
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        await applicant.ApplyAsync(activity.Id, message: "Partante !");

        var received = await organizer.GetReceivedApplicationsAsync(activity.Id);

        var application = Assert.Single(received);
        Assert.Equal(("Léa", 22, "Partante !"), (application.ApplicantFirstName, application.ApplicantAge, application.Message));
    }

    [Fact]
    public async Task ReceivedApplications_AsAnotherUser_ReturnsNotOrganizer()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var otherUser = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());

        var response = await otherUser.GetAsync($"/activities/{activity.Id}/applications");

        await response.AssertBusinessErrorAsync(HttpStatusCode.Forbidden, ApplicationErrorCodes.NotOrganizer);
    }

    // R4
    [Fact]
    public async Task Accept_PendingApplication_FillsActivity()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var applicationId = await applicant.ApplySuccessfullyAsync(activity.Id);

        var response = await organizer.AcceptAsync(applicationId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ActivityStatus.Filled, (await organizer.GetActivityAsync(activity.Id)).Status);
        Assert.Equal(MyApplicationStatus.Accepted, Assert.Single(await applicant.GetMyApplicationsAsync()).Status);
    }

    // R4
    [Fact]
    public async Task Accept_ExpiresOtherPendingApplications()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var chosen = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var other = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Jules");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var chosenApplicationId = await chosen.ApplySuccessfullyAsync(activity.Id);
        var otherApplicationId = await other.ApplySuccessfullyAsync(activity.Id);

        await organizer.AcceptAsync(chosenApplicationId);

        var otherStatus = await factory.WithDatabaseAsync(db =>
            db.Applications.Where(a => a.Id == otherApplicationId).Select(a => a.Status).SingleAsync());
        Assert.Equal(ApplicationStatus.Expired, otherStatus);
    }

    // R11
    [Fact]
    public async Task Accept_LessThan6HoursBeforeStart_ReturnsShortNoticeWarning()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity() with { StartsAt = OnygoApiFactory.Now.AddHours(3) });
        var applicationId = await applicant.ApplySuccessfullyAsync(activity.Id);

        var response = await organizer.AcceptAsync(applicationId);

        var accepted = await response.Content.ReadFromJsonAsync<AcceptedApplicationResponse>(TestJson.Options);
        Assert.True(accepted!.ShortNoticeWarning);
    }

    [Fact]
    public async Task Accept_MoreThan6HoursBeforeStart_ReturnsNoShortNoticeWarning()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var applicationId = await applicant.ApplySuccessfullyAsync(activity.Id);

        var response = await organizer.AcceptAsync(applicationId);

        var accepted = await response.Content.ReadFromJsonAsync<AcceptedApplicationResponse>(TestJson.Options);
        Assert.False(accepted!.ShortNoticeWarning);
    }

    [Fact]
    public async Task Accept_ByTheApplicant_ReturnsNotOrganizer()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var applicationId = await applicant.ApplySuccessfullyAsync(activity.Id);

        var response = await applicant.AcceptAsync(applicationId);

        await response.AssertBusinessErrorAsync(HttpStatusCode.Forbidden, ApplicationErrorCodes.NotOrganizer);
    }

    [Fact]
    public async Task Accept_RefusedApplication_ReturnsApplicationNotPending()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var applicationId = await applicant.ApplySuccessfullyAsync(activity.Id);
        await organizer.RefuseAsync(applicationId);

        var response = await organizer.AcceptAsync(applicationId);

        await response.AssertBusinessErrorAsync(HttpStatusCode.Conflict, ApplicationErrorCodes.ApplicationNotPending);
    }

    [Fact]
    public async Task Accept_AfterActivityStarted_ReturnsActivityAlreadyStarted()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var applicationId = await applicant.ApplySuccessfullyAsync(activity.Id);
        await factory.WithDatabaseAsync(db => db.Activities
            .Where(a => a.Id == activity.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.StartsAt, OnygoApiFactory.Now.AddMinutes(-5))));

        var response = await organizer.AcceptAsync(applicationId);

        await response.AssertBusinessErrorAsync(HttpStatusCode.Conflict, ApplicationErrorCodes.ActivityAlreadyStarted);
    }

    [Fact]
    public async Task Accept_UnknownApplication_ReturnsNotFound()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();

        var response = await organizer.AcceptAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Refuse_ByTheApplicant_ReturnsNotOrganizer()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var applicationId = await applicant.ApplySuccessfullyAsync(activity.Id);

        var response = await applicant.RefuseAsync(applicationId);

        await response.AssertBusinessErrorAsync(HttpStatusCode.Forbidden, ApplicationErrorCodes.NotOrganizer);
    }

    // R10
    [Fact]
    public async Task Refuse_KeepsTheApplicationInDatabaseAsRefused()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var applicationId = await applicant.ApplySuccessfullyAsync(activity.Id);

        var response = await organizer.RefuseAsync(applicationId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var status = await factory.WithDatabaseAsync(db =>
            db.Applications.Where(a => a.Id == applicationId).Select(a => a.Status).SingleAsync());
        Assert.Equal(ApplicationStatus.Refused, status);
    }

    [Fact]
    public async Task Refuse_RemovesApplicationFromOrganizerList()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var applicationId = await applicant.ApplySuccessfullyAsync(activity.Id);

        await organizer.RefuseAsync(applicationId);

        Assert.Empty(await organizer.GetReceivedApplicationsAsync(activity.Id));
    }
}

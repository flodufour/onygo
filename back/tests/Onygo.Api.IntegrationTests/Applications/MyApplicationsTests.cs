using Onygo.Api.Applications;
using Onygo.Api.IntegrationTests.Infrastructure;
using static Onygo.Api.IntegrationTests.Activities.ActivityRequests;

namespace Onygo.Api.IntegrationTests.Applications;

/// <summary>
/// R9 : refus silencieux. Le candidat ne voit jamais le statut Refused, et ses candidatures refusées
/// ou expirées disparaissent de « Mes candidatures ».
/// </summary>
[Collection(ApiCollection.Name)]
public class MyApplicationsTests(OnygoApiFactory factory)
{
    private readonly AuthApiClient _auth = new(factory);

    [Fact]
    public async Task MyApplications_ListsPendingApplicationWithActivityTitle()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        await applicant.ApplySuccessfullyAsync(activity.Id);

        var applications = await applicant.GetMyApplicationsAsync();

        var application = Assert.Single(applications);
        Assert.Equal((activity.Id, activity.Title, MyApplicationStatus.Pending), (application.ActivityId, application.ActivityTitle, application.Status));
    }

    [Fact]
    public async Task MyApplications_AfterRefusal_NoLongerShowsTheApplication()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var applicationId = await applicant.ApplySuccessfullyAsync(activity.Id);
        await organizer.RefuseAsync(applicationId);

        var response = await applicant.GetAsync("/applications/mine");

        var json = await response.Content.ReadAsStringAsync();
        Assert.Equal("[]", json);
    }

    [Fact]
    public async Task MyApplications_AfterAnotherApplicantWasAccepted_NoLongerShowsTheExpiredApplication()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var chosen = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var notChosen = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Jules");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var chosenApplicationId = await chosen.ApplySuccessfullyAsync(activity.Id);
        await notChosen.ApplySuccessfullyAsync(activity.Id);
        await organizer.AcceptAsync(chosenApplicationId);

        var applications = await notChosen.GetMyApplicationsAsync();

        Assert.Empty(applications);
    }
}

using System.Net;
using Microsoft.EntityFrameworkCore;
using Onygo.Api.Applications;
using Onygo.Api.IntegrationTests.Infrastructure;
using static Onygo.Api.IntegrationTests.Activities.ActivityRequests;

namespace Onygo.Api.IntegrationTests.Applications;

/// <summary>
/// R1 et R4 face à des requêtes réellement simultanées : sans verrou, plusieurs requêtes liraient
/// le même comptage et dépasseraient la limite.
/// </summary>
[Collection(ApiCollection.Name)]
public class ApplicationConcurrencyTests(OnygoApiFactory factory)
{
    private readonly AuthApiClient _auth = new(factory);

    // R1
    [Fact]
    public async Task Apply_SixSimultaneousApplicants_OnlyThreeApplicationsAreRecorded()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var applicants = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => _auth.CreateSignedInClientWithProfileAsync()));

        var responses = await Task.WhenAll(applicants.Select(applicant => applicant.ApplyAsync(activity.Id)));

        Assert.Equal(3, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        var pendingCount = await factory.WithDatabaseAsync(db =>
            db.Applications.CountAsync(a => a.ActivityId == activity.Id && a.Status == ApplicationStatus.Pending));
        Assert.Equal(3, pendingCount);
    }

    // R4
    [Fact]
    public async Task Accept_TwoApplicationsSimultaneously_OnlyOneIsAccepted()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var firstApplicationId = await (await _auth.CreateSignedInClientWithProfileAsync()).ApplySuccessfullyAsync(activity.Id);
        var secondApplicationId = await (await _auth.CreateSignedInClientWithProfileAsync()).ApplySuccessfullyAsync(activity.Id);

        var responses = await Task.WhenAll(organizer.AcceptAsync(firstApplicationId), organizer.AcceptAsync(secondApplicationId));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        var acceptedCount = await factory.WithDatabaseAsync(db =>
            db.Applications.CountAsync(a => a.ActivityId == activity.Id && a.Status == ApplicationStatus.Accepted));
        Assert.Equal(1, acceptedCount);
    }
}

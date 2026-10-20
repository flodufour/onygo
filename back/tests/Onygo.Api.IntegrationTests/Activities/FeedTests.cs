using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Onygo.Api.Activities;
using Onygo.Api.IntegrationTests.Infrastructure;
using static Onygo.Api.IntegrationTests.Activities.ActivityRequests;

namespace Onygo.Api.IntegrationTests.Activities;

/// <summary>
/// Feed (CLAUDE.md racine §2.5). La base est partagée : chaque test vérifie seulement ses propres activités.
/// </summary>
[Collection(ApiCollection.Name)]
public class FeedTests(OnygoApiFactory factory)
{
    private readonly AuthApiClient _auth = new(factory);

    [Fact]
    public async Task Feed_IncludesOpenFutureActivityOfAnotherUser()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Camille", age: 21);
        var viewer = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());

        var feed = await viewer.ReadWholeFeedAsync();

        var shown = Assert.Single(feed, a => a.Id == activity.Id);
        Assert.Equal(new OrganizerResponse("Camille", 21), shown.Organizer);
    }

    [Fact]
    public async Task Feed_NeverRevealsExactLocation()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var viewer = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        await organizer.CreateActivityAsync(AValidActivity());

        var response = await viewer.GetAsync($"/activities/feed?limit={FeedRules.MaxPageSize}");

        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Biscuiterie", json);
        Assert.Contains("\"exactLocation\":null", json);
    }

    [Fact]
    public async Task Feed_ExcludesOwnActivities()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var activity = await organizer.CreateActivityAsync(AValidActivity());

        var feed = await organizer.ReadWholeFeedAsync();

        Assert.DoesNotContain(feed, a => a.Id == activity.Id);
    }

    // R3
    [Fact]
    public async Task Feed_ExcludesActivityAlreadyAppliedTo()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        await applicant.ApplySuccessfullyAsync(activity.Id);

        var feed = await applicant.ReadWholeFeedAsync();

        Assert.DoesNotContain(feed, a => a.Id == activity.Id);
    }

    // R3 / R9 : un refusé ne revoit plus l'activité.
    [Fact]
    public async Task Feed_AfterRefusal_StillExcludesActivityForTheRefusedApplicant()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        await organizer.RefuseAsync(await applicant.ApplySuccessfullyAsync(activity.Id));

        var feed = await applicant.ReadWholeFeedAsync();

        Assert.DoesNotContain(feed, a => a.Id == activity.Id);
    }

    // R1
    [Fact]
    public async Task Feed_ExcludesActivityWith3PendingApplications()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var viewer = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        await (await _auth.CreateSignedInClientWithProfileAsync()).ApplySuccessfullyAsync(activity.Id);
        await (await _auth.CreateSignedInClientWithProfileAsync()).ApplySuccessfullyAsync(activity.Id);
        await (await _auth.CreateSignedInClientWithProfileAsync()).ApplySuccessfullyAsync(activity.Id);

        var feed = await viewer.ReadWholeFeedAsync();

        Assert.DoesNotContain(feed, a => a.Id == activity.Id);
    }

    // R2
    [Fact]
    public async Task Feed_AfterARefusalFreedASpot_ShowsActivityAgain()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var viewer = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        var refusedApplicationId = await (await _auth.CreateSignedInClientWithProfileAsync()).ApplySuccessfullyAsync(activity.Id);
        await (await _auth.CreateSignedInClientWithProfileAsync()).ApplySuccessfullyAsync(activity.Id);
        await (await _auth.CreateSignedInClientWithProfileAsync()).ApplySuccessfullyAsync(activity.Id);
        await organizer.RefuseAsync(refusedApplicationId);

        var feed = await viewer.ReadWholeFeedAsync();

        Assert.Contains(feed, a => a.Id == activity.Id);
    }

    [Fact]
    public async Task Feed_ExcludesFilledActivity()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var viewer = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        await organizer.AcceptAsync(await (await _auth.CreateSignedInClientWithProfileAsync()).ApplySuccessfullyAsync(activity.Id));

        var feed = await viewer.ReadWholeFeedAsync();

        Assert.DoesNotContain(feed, a => a.Id == activity.Id);
    }

    // R5 : plus possible d'y postuler.
    [Fact]
    public async Task Feed_ExcludesActivityStartingWithin1Hour()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var viewer = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activity = await organizer.CreateActivityAsync(AValidActivity());
        await factory.SetActivityStartAsync(activity.Id, OnygoApiFactory.Now.AddHours(1));

        var feed = await viewer.ReadWholeFeedAsync();

        Assert.DoesNotContain(feed, a => a.Id == activity.Id);
    }

    // R11
    [Fact]
    public async Task Feed_FlagsActivityStartingInLessThan6Hours()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var viewer = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var soon = await organizer.CreateActivityAsync(AValidActivity() with { StartsAt = OnygoApiFactory.Now.AddHours(5) });
        var later = await organizer.CreateActivityAsync(AValidActivity() with { StartsAt = OnygoApiFactory.Now.AddHours(7) });

        var feed = await viewer.ReadWholeFeedAsync();

        Assert.True(feed.Single(a => a.Id == soon.Id).ShortNoticeWarning);
        Assert.False(feed.Single(a => a.Id == later.Id).ShortNoticeWarning);
    }

    [Fact]
    public async Task Feed_OrdersActivitiesFromTheSoonest()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var viewer = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var inThreeDays = await organizer.CreateActivityAsync(AValidActivity() with { StartsAt = OnygoApiFactory.Now.AddDays(3) });
        var tomorrow = await organizer.CreateActivityAsync(AValidActivity() with { StartsAt = OnygoApiFactory.Now.AddDays(1) });
        var inTwoDays = await organizer.CreateActivityAsync(AValidActivity() with { StartsAt = OnygoApiFactory.Now.AddDays(2) });
        var mine = new[] { inThreeDays.Id, tomorrow.Id, inTwoDays.Id };

        var feed = await viewer.ReadWholeFeedAsync();

        var myActivitiesInFeedOrder = feed.Where(a => mine.Contains(a.Id)).Select(a => a.Id);
        Assert.Equal([tomorrow.Id, inTwoDays.Id, inThreeDays.Id], myActivitiesInFeedOrder);
    }

    // Plusieurs activités à la même heure, lues 2 par 2 : aucune ne doit être sautée ni répétée.
    [Fact]
    public async Task Feed_ReadPageByPage_ReturnsEachActivityExactlyOnce()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var viewer = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var sameTime = AValidActivity() with { StartsAt = OnygoApiFactory.Now.AddDays(4).AddMinutes(17) };
        var mine = (await organizer.CreateActivitiesAsync(sameTime, count: 5)).Select(a => a.Id).ToList();

        var feed = await viewer.ReadWholeFeedAsync(limit: 2);

        Assert.Equal(feed.Count, feed.Select(a => a.Id).Distinct().Count());
        Assert.Equal(mine.Order(), feed.Where(a => mine.Contains(a.Id)).Select(a => a.Id).Order());
    }

    [Fact]
    public async Task Feed_WithInvalidCursor_ReturnsInvalidCursor()
    {
        var viewer = await _auth.CreateSignedInClientWithProfileAsync();

        var response = await viewer.GetAsync("/activities/feed?cursor=not-a-cursor");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal([ActivityErrorCodes.InvalidCursor], problem!.Errors["cursor"]);
    }

    [Fact]
    public async Task Feed_WithoutAccessToken_ReturnsUnauthorized()
    {
        var anonymousClient = factory.CreateClient();

        var response = await anonymousClient.GetAsync("/activities/feed");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

}

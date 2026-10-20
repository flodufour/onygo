using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Onygo.Api.Activities;
using Onygo.Api.Common;
using Onygo.Api.IntegrationTests.Infrastructure;
using static Onygo.Api.IntegrationTests.Activities.ActivityRequests;

namespace Onygo.Api.IntegrationTests.Activities;

[Collection(ApiCollection.Name)]
public class ActivityCreationTests(OnygoApiFactory factory)
{
    private readonly AuthApiClient _auth = new(factory);

    [Fact]
    public async Task CreateActivity_WithValidData_ReturnsOpenActivityWithOrganizer()
    {
        var client = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Camille", age: 21);

        var response = await client.PostAsJsonAsync("/activities", AValidActivity(), TestJson.Options);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var activity = await response.Content.ReadFromJsonAsync<ActivityResponse>(TestJson.Options);
        Assert.Equal(ActivityStatus.Open, activity!.Status);
        Assert.Equal("Café au Lieu Unique", activity.Title);
        Assert.Equal(new OrganizerResponse("Camille", 21), activity.Organizer);
        Assert.Equal($"/activities/{activity.Id}", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task CreateActivity_WithoutProfile_ReturnsProfileRequired()
    {
        var client = await _auth.CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("/activities", AValidActivity(), TestJson.Options);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(ActivityErrorCodes.ProfileRequired, problem!.Extensions["code"]!.ToString());
    }

    [Fact]
    public async Task CreateActivity_WithoutAccessToken_ReturnsUnauthorized()
    {
        var anonymousClient = factory.CreateClient();

        var response = await anonymousClient.PostAsJsonAsync("/activities", AValidActivity(), TestJson.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateActivity_StartingInThePast_ReturnsStartsAtInPast()
    {
        var client = await _auth.CreateSignedInClientWithProfileAsync();
        var request = AValidActivity() with { StartsAt = OnygoApiFactory.Now.AddMinutes(-1) };

        var response = await client.PostAsJsonAsync("/activities", request, TestJson.Options);

        await AssertValidationErrorAsync(response, "startsAt", ActivityErrorCodes.StartsAtInPast);
    }

    // R5 : les candidatures fermant 2 h avant le début, personne ne pourrait y postuler.
    [Fact]
    public async Task CreateActivity_StartingIn2Hours_ReturnsStartsAtTooSoon()
    {
        var client = await _auth.CreateSignedInClientWithProfileAsync();
        var request = AValidActivity() with { StartsAt = OnygoApiFactory.Now.AddHours(2) };

        var response = await client.PostAsJsonAsync("/activities", request, TestJson.Options);

        await AssertValidationErrorAsync(response, "startsAt", ActivityErrorCodes.StartsAtTooSoon);
    }

    [Fact]
    public async Task CreateActivity_Exactly30DaysAhead_ReturnsCreated()
    {
        var client = await _auth.CreateSignedInClientWithProfileAsync();
        var request = AValidActivity() with { StartsAt = OnygoApiFactory.Now.AddDays(30) };

        var response = await client.PostAsJsonAsync("/activities", request, TestJson.Options);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateActivity_MoreThan30DaysAhead_ReturnsStartsAtTooFarAhead()
    {
        var client = await _auth.CreateSignedInClientWithProfileAsync();
        var request = AValidActivity() with { StartsAt = OnygoApiFactory.Now.AddDays(30).AddMinutes(1) };

        var response = await client.PostAsJsonAsync("/activities", request, TestJson.Options);

        await AssertValidationErrorAsync(response, "startsAt", ActivityErrorCodes.StartsAtTooFarAhead);
    }

    [Fact]
    public async Task CreateActivity_OfTypeOtherWithCustomLabel_ReturnsCustomType()
    {
        var client = await _auth.CreateSignedInClientWithProfileAsync();
        var request = AValidActivity() with { Type = ActivityType.Other, CustomType = " Escape game " };

        var response = await client.PostAsJsonAsync("/activities", request, TestJson.Options);

        var activity = await response.Content.ReadFromJsonAsync<ActivityResponse>(TestJson.Options);
        Assert.Equal(ActivityType.Other, activity!.Type);
        Assert.Equal("Escape game", activity.CustomType);
    }

    [Fact]
    public async Task CreateActivity_OfTypeOtherWithoutCustomLabel_ReturnsCustomTypeRequired()
    {
        var client = await _auth.CreateSignedInClientWithProfileAsync();
        var request = AValidActivity() with { Type = ActivityType.Other, CustomType = null };

        var response = await client.PostAsJsonAsync("/activities", request, TestJson.Options);

        await AssertValidationErrorAsync(response, "customType", ActivityErrorCodes.CustomTypeRequired);
    }

    [Fact]
    public async Task CreateActivity_WithoutHook_ReturnsHookRequired()
    {
        var client = await _auth.CreateSignedInClientWithProfileAsync();
        var request = AValidActivity() with { Hook = "" };

        var response = await client.PostAsJsonAsync("/activities", request, TestJson.Options);

        await AssertValidationErrorAsync(response, "hook", ActivityErrorCodes.HookRequired);
    }

    [Fact]
    public async Task CreateActivity_PublicAreaIsOffsetFromExactLocationButStillContainsIt()
    {
        var client = await _auth.CreateSignedInClientWithProfileAsync();

        var response = await client.PostAsJsonAsync("/activities", AValidActivity(), TestJson.Options);

        var activity = await response.Content.ReadFromJsonAsync<ActivityResponse>(TestJson.Options);
        var exactPoint = new GeoPoint(LieuUniqueLatitude, LieuUniqueLongitude);
        var publicAreaCenter = new GeoPoint(activity!.PublicArea.Latitude, activity.PublicArea.Longitude);
        var offsetMeters = exactPoint.DistanceInMetersTo(publicAreaCenter);
        Assert.InRange(offsetMeters, ActivityRules.MinPublicAreaOffsetMeters - 1, ActivityRules.MaxPublicAreaOffsetMeters + 1);
        Assert.True(offsetMeters < activity.PublicArea.RadiusMeters);
    }

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response, string field, string expectedCode)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal([expectedCode], problem!.Errors[field]);
    }
}

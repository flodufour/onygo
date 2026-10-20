using System.Net;
using System.Net.Http.Json;
using Onygo.Api.Activities;
using Onygo.Api.IntegrationTests.Infrastructure;
using static Onygo.Api.IntegrationTests.Activities.ActivityRequests;

namespace Onygo.Api.IntegrationTests.Activities;

[Collection(ApiCollection.Name)]
public class ActivityVisibilityTests(OnygoApiFactory factory)
{
    private readonly AuthApiClient _auth = new(factory);

    [Fact]
    public async Task GetActivity_AsOrganizer_IncludesExactLocation()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var activityId = await CreateActivityAsync(organizer);

        var activity = await organizer.GetFromJsonAsync<ActivityResponse>($"/activities/{activityId}", TestJson.Options);

        Assert.Equal(
            new ExactLocationResponse(LieuUniqueAddress, LieuUniqueLatitude, LieuUniqueLongitude),
            activity!.ExactLocation);
    }

    [Fact]
    public async Task GetActivity_AsAnotherUser_HidesExactLocation()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var otherUser = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activityId = await CreateActivityAsync(organizer);

        var response = await otherUser.GetAsync($"/activities/{activityId}");

        var json = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Biscuiterie", json);
        Assert.DoesNotContain(LieuUniqueLatitude.ToString(System.Globalization.CultureInfo.InvariantCulture), json);
    }

    [Fact]
    public async Task GetActivity_AsAnotherUser_ReturnsSamePublicAreaOnEveryRequest()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var otherUser = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activityId = await CreateActivityAsync(organizer);
        var first = await otherUser.GetFromJsonAsync<ActivityResponse>($"/activities/{activityId}", TestJson.Options);

        var second = await otherUser.GetFromJsonAsync<ActivityResponse>($"/activities/{activityId}", TestJson.Options);

        Assert.Equal(first!.PublicArea, second!.PublicArea);
    }

    [Fact]
    public async Task GetActivity_AsAcceptedApplicant_IncludesExactLocation()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activityId = await CreateActivityAsync(organizer);
        await organizer.AcceptAsync(await applicant.ApplySuccessfullyAsync(activityId));

        var activity = await applicant.GetActivityAsync(activityId);

        Assert.Equal(LieuUniqueAddress, activity.ExactLocation?.Address);
    }

    [Fact]
    public async Task GetActivity_AsRefusedApplicant_HidesExactLocation()
    {
        var organizer = await _auth.CreateSignedInClientWithProfileAsync();
        var applicant = await _auth.CreateSignedInClientWithProfileAsync(firstName: "Léa");
        var activityId = await CreateActivityAsync(organizer);
        await organizer.RefuseAsync(await applicant.ApplySuccessfullyAsync(activityId));

        var activity = await applicant.GetActivityAsync(activityId);

        Assert.Null(activity.ExactLocation);
    }

    [Fact]
    public async Task GetActivity_WithUnknownId_ReturnsNotFound()
    {
        var client = await _auth.CreateSignedInClientWithProfileAsync();

        var response = await client.GetAsync($"/activities/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<Guid> CreateActivityAsync(HttpClient organizer)
    {
        var response = await organizer.PostAsJsonAsync("/activities", AValidActivity(), TestJson.Options);
        response.EnsureSuccessStatusCode();
        var activity = await response.Content.ReadFromJsonAsync<ActivityResponse>(TestJson.Options);
        return activity!.Id;
    }
}

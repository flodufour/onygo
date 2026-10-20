using System.Net.Http.Json;
using Onygo.Api.Activities;
using Onygo.Api.Applications;

namespace Onygo.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Appels aux routes d'activités et de candidatures, comme le ferait l'appli.
/// </summary>
public static class ActivityApi
{
    public static async Task<ActivityResponse> CreateActivityAsync(this HttpClient organizer, CreateActivityRequest request)
    {
        var response = await organizer.PostAsJsonAsync("/activities", request, TestJson.Options);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ActivityResponse>(TestJson.Options))!;
    }

    public static async Task<List<ActivityResponse>> CreateActivitiesAsync(
        this HttpClient organizer,
        CreateActivityRequest request,
        int count)
    {
        var activities = new List<ActivityResponse>();
        for (var i = 0; i < count; i++)
        {
            activities.Add(await organizer.CreateActivityAsync(request));
        }

        return activities;
    }

    public static Task<HttpResponseMessage> ApplyAsync(this HttpClient applicant, Guid activityId, string? message = null) =>
        applicant.PostAsJsonAsync($"/activities/{activityId}/applications", new { message });

    public static async Task<Guid> ApplySuccessfullyAsync(this HttpClient applicant, Guid activityId)
    {
        var response = await applicant.ApplyAsync(activityId);
        response.EnsureSuccessStatusCode();
        var application = await response.Content.ReadFromJsonAsync<MyApplicationResponse>(TestJson.Options);
        return application!.Id;
    }

    public static Task<HttpResponseMessage> AcceptAsync(this HttpClient organizer, Guid applicationId) =>
        organizer.PostAsync($"/applications/{applicationId}/accept", content: null);

    public static Task<HttpResponseMessage> RefuseAsync(this HttpClient organizer, Guid applicationId) =>
        organizer.PostAsync($"/applications/{applicationId}/refuse", content: null);

    public static async Task<List<MyApplicationResponse>> GetMyApplicationsAsync(this HttpClient applicant) =>
        (await applicant.GetFromJsonAsync<List<MyApplicationResponse>>("/applications/mine", TestJson.Options))!;

    public static async Task<List<ReceivedApplicationResponse>> GetReceivedApplicationsAsync(
        this HttpClient organizer,
        Guid activityId) =>
        (await organizer.GetFromJsonAsync<List<ReceivedApplicationResponse>>(
            $"/activities/{activityId}/applications",
            TestJson.Options))!;

    public static async Task<ActivityResponse> GetActivityAsync(this HttpClient client, Guid activityId) =>
        (await client.GetFromJsonAsync<ActivityResponse>($"/activities/{activityId}", TestJson.Options))!;

    public static async Task<FeedPageResponse> GetFeedPageAsync(this HttpClient viewer, string? cursor = null, int? limit = null)
    {
        var query = new List<string>();
        if (cursor is not null)
        {
            query.Add($"cursor={Uri.EscapeDataString(cursor)}");
        }

        if (limit is not null)
        {
            query.Add($"limit={limit}");
        }

        var url = query.Count == 0 ? "/activities/feed" : $"/activities/feed?{string.Join('&', query)}";
        return (await viewer.GetFromJsonAsync<FeedPageResponse>(url, TestJson.Options))!;
    }

    /// <summary>
    /// Parcourt toutes les pages du feed. La base est partagée entre les tests : chaque test ne vérifie
    /// que ses propres activités dans le résultat.
    /// </summary>
    public static async Task<List<ActivityResponse>> ReadWholeFeedAsync(this HttpClient viewer, int? limit = null)
    {
        var activities = new List<ActivityResponse>();
        string? cursor = null;
        do
        {
            var page = await viewer.GetFeedPageAsync(cursor, limit ?? FeedRules.MaxPageSize);
            activities.AddRange(page.Activities);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        return activities;
    }
}

using System.Net.Http.Json;

namespace Onygo.Api.IntegrationTests.Infrastructure;

public static class ProfileApi
{
    public static async Task CreateProfileAsync(this HttpClient client, string firstName = "Camille", int age = 25)
    {
        var birthDate = OnygoApiFactory.Today.AddYears(-age);
        (await client.PostAsJsonAsync("/profile", new { firstName, birthDate })).EnsureSuccessStatusCode();
    }
}

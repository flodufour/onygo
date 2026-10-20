using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.BearerToken;

namespace Onygo.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Appelle les routes d'authentification d'Identity comme le ferait un front.
/// </summary>
public sealed class AuthApiClient(OnygoApiFactory factory, HttpClient http)
{
    public const string ValidPassword = "Onygo-Passw0rd!";

    public AuthApiClient(OnygoApiFactory factory)
        : this(factory, factory.CreateClient())
    {
    }

    public HttpClient Http { get; } = http;

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@onygo.test";

    public Task<HttpResponseMessage> RegisterAsync(string email, string password = ValidPassword) =>
        Http.PostAsJsonAsync("/auth/register", new { email, password });

    public Task<HttpResponseMessage> ConfirmEmailAsync(string email) =>
        Http.GetAsync(factory.EmailSender.ConfirmationLinkSentTo(email));

    public Task<HttpResponseMessage> LoginAsync(string email, string password = ValidPassword) =>
        Http.PostAsJsonAsync("/auth/login", new { email, password });

    public Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        Http.PostAsJsonAsync("/auth/refresh", new { refreshToken });

    public Task<HttpResponseMessage> ForgotPasswordAsync(string email) =>
        Http.PostAsJsonAsync("/auth/forgotPassword", new { email });

    public Task<HttpResponseMessage> ResetPasswordAsync(string email, string resetCode, string newPassword) =>
        Http.PostAsJsonAsync("/auth/resetPassword", new { email, resetCode, newPassword });

    public Task<HttpResponseMessage> GetAccountInfoAsync(string? accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/manage/info");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return Http.SendAsync(request);
    }

    public async Task<string> RegisterConfirmedUserAsync()
    {
        var email = NewEmail();
        (await RegisterAsync(email)).EnsureSuccessStatusCode();
        (await ConfirmEmailAsync(email)).EnsureSuccessStatusCode();
        return email;
    }

    public async Task<AccessTokenResponse> LoginConfirmedUserAsync()
    {
        var email = await RegisterConfirmedUserAsync();
        var response = await LoginAsync(email);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccessTokenResponse>())!;
    }

    /// <summary>Client HTTP d'un nouvel utilisateur confirmé et connecté (jeton Bearer déjà positionné).</summary>
    public async Task<HttpClient> CreateSignedInClientAsync()
    {
        var tokens = await LoginConfirmedUserAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }
}

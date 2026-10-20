using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Onygo.Api.IntegrationTests.Infrastructure;

namespace Onygo.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class UnsupportedAuthFeaturesTests(OnygoApiFactory factory)
{
    private readonly AuthApiClient _auth = new(factory);

    [Fact]
    public async Task Login_WithCookies_ReturnsBadRequestWithoutSettingCookie()
    {
        var email = await _auth.RegisterConfirmedUserAsync();

        var response = await _auth.Http.PostAsJsonAsync(
            "/auth/login?useCookies=true",
            new { email, password = AuthApiClient.ValidPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task TwoFactorRoute_WithAccessToken_ReturnsNotFound()
    {
        var tokens = await _auth.LoginConfirmedUserAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/manage/2fa")
        {
            Content = JsonContent.Create(new { enable = true }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await _auth.Http.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

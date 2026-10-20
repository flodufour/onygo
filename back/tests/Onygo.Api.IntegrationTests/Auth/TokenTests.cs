using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity.Data;
using Onygo.Api.IntegrationTests.Infrastructure;

namespace Onygo.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class TokenTests(OnygoApiFactory factory)
{
    private readonly AuthApiClient _auth = new(factory);

    [Fact]
    public async Task Refresh_WithValidRefreshToken_ReturnsUsableAccessToken()
    {
        var tokens = await _auth.LoginConfirmedUserAsync();

        var response = await _auth.RefreshAsync(tokens.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = await response.Content.ReadFromJsonAsync<AccessTokenResponse>();
        var infoResponse = await _auth.GetAccountInfoAsync(refreshed!.AccessToken);
        Assert.Equal(HttpStatusCode.OK, infoResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithInvalidRefreshToken_ReturnsUnauthorized()
    {
        var response = await _auth.RefreshAsync("not-a-refresh-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AccountInfo_WithoutAccessToken_ReturnsUnauthorized()
    {
        var response = await _auth.GetAccountInfoAsync(accessToken: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AccountInfo_WithAccessToken_ReturnsConfirmedEmail()
    {
        var email = await _auth.RegisterConfirmedUserAsync();
        var login = await _auth.LoginAsync(email);
        var tokens = await login.Content.ReadFromJsonAsync<AccessTokenResponse>();

        var response = await _auth.GetAccountInfoAsync(tokens!.AccessToken);

        var info = await response.Content.ReadFromJsonAsync<InfoResponse>();
        Assert.Equal(email, info!.Email);
        Assert.True(info.IsEmailConfirmed);
    }
}

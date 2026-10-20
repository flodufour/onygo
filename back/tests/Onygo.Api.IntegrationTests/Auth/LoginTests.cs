using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Onygo.Api.IntegrationTests.Infrastructure;

namespace Onygo.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class LoginTests(OnygoApiFactory factory)
{
    private readonly AuthApiClient _auth = new(factory);

    [Fact]
    public async Task Login_WhenEmailIsNotConfirmed_ReturnsUnauthorized()
    {
        var email = AuthApiClient.NewEmail();
        await _auth.RegisterAsync(email);

        var response = await _auth.LoginAsync(email);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_AfterEmailConfirmation_ReturnsBearerTokens()
    {
        var email = await _auth.RegisterConfirmedUserAsync();

        var response = await _auth.LoginAsync(email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<AccessTokenResponse>();
        Assert.Equal("Bearer", tokens!.TokenType);
        Assert.NotEmpty(tokens.AccessToken);
        Assert.NotEmpty(tokens.RefreshToken);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var email = await _auth.RegisterConfirmedUserAsync();

        var response = await _auth.LoginAsync(email, password: "Wrong-Passw0rd!");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Onygo.Api.IntegrationTests.Infrastructure;

namespace Onygo.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class RateLimitingTests(OnygoApiFactory factory)
{
    [Fact]
    public async Task Login_BeyondSignInLimit_ReturnsTooManyRequests()
    {
        await using var limitedApi = WithLimits(signInPermitsPerMinute: 2, emailPermitsPerHour: 10);
        var auth = new AuthApiClient(factory, limitedApi.CreateClient());
        var email = AuthApiClient.NewEmail();
        await auth.LoginAsync(email);
        await auth.LoginAsync(email);

        var response = await auth.LoginAsync(email);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_BeyondEmailLimit_ReturnsTooManyRequests()
    {
        await using var limitedApi = WithLimits(signInPermitsPerMinute: 10, emailPermitsPerHour: 1);
        var auth = new AuthApiClient(factory, limitedApi.CreateClient());
        var email = AuthApiClient.NewEmail();
        await auth.ForgotPasswordAsync(email);

        var response = await auth.ForgotPasswordAsync(email);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_DoesNotConsumeSignInLimit()
    {
        await using var limitedApi = WithLimits(signInPermitsPerMinute: 1, emailPermitsPerHour: 10);
        var auth = new AuthApiClient(factory, limitedApi.CreateClient());
        var email = AuthApiClient.NewEmail();
        await auth.ForgotPasswordAsync(email);

        var response = await auth.LoginAsync(email);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private WebApplicationFactory<Program> WithLimits(int signInPermitsPerMinute, int emailPermitsPerHour) =>
        factory.WithWebHostBuilder(builder => builder
            .UseSetting("RateLimiting:Auth:SignInPermitsPerMinute", signInPermitsPerMinute.ToString())
            .UseSetting("RateLimiting:Auth:EmailPermitsPerHour", emailPermitsPerHour.ToString()));
}

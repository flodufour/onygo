using Onygo.Api.Auth;

namespace Onygo.Api.UnitTests.Auth;

public class AuthRateLimitingTests
{
    [Theory]
    [InlineData("/auth/forgotPassword")]
    [InlineData("/auth/resendConfirmationEmail")]
    public void PolicyFor_RouteSendingAnEmail_ReturnsEmailPolicy(string routePattern)
    {
        var policy = AuthRateLimiting.PolicyFor(routePattern);

        Assert.Equal(AuthRateLimiting.EmailPolicy, policy);
    }

    [Theory]
    [InlineData("/auth/login")]
    [InlineData("/auth/register")]
    [InlineData("/auth/refresh")]
    [InlineData("/auth/resetPassword")]
    public void PolicyFor_OtherPublicAuthRoute_ReturnsSignInPolicy(string routePattern)
    {
        var policy = AuthRateLimiting.PolicyFor(routePattern);

        Assert.Equal(AuthRateLimiting.SignInPolicy, policy);
    }
}

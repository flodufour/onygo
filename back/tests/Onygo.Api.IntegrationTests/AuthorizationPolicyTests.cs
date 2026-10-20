using System.Net;
using Onygo.Api.IntegrationTests.Infrastructure;

namespace Onygo.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthorizationPolicyTests(OnygoApiFactory factory)
{
    // Une route sans règle d'autorisation explicite exige un utilisateur connecté (politique de repli).
    [Fact]
    public async Task RouteWithoutExplicitPolicy_WithoutAccessToken_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/route-without-explicit-policy");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

using System.Net;
using Onygo.Api.IntegrationTests.Infrastructure;

namespace Onygo.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class RegistrationTests(OnygoApiFactory factory)
{
    private readonly AuthApiClient _auth = new(factory);

    [Fact]
    public async Task Register_WithNewEmail_SendsConfirmationLink()
    {
        var email = AuthApiClient.NewEmail();

        var response = await _auth.RegisterAsync(email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(factory.EmailSender.HasSentConfirmationLinkTo(email));
    }

    [Fact]
    public async Task Register_WithAlreadyUsedEmail_ReturnsBadRequest()
    {
        var email = await _auth.RegisterConfirmedUserAsync();

        var response = await _auth.RegisterAsync(email);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsBadRequest()
    {
        var response = await _auth.RegisterAsync(AuthApiClient.NewEmail(), password: "weak");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

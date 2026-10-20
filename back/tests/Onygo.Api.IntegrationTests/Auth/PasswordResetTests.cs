using System.Net;
using Onygo.Api.IntegrationTests.Infrastructure;

namespace Onygo.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class PasswordResetTests(OnygoApiFactory factory)
{
    private const string NewPassword = "New-Onygo-Passw0rd!";

    private readonly AuthApiClient _auth = new(factory);

    [Fact]
    public async Task ResetPassword_WithCodeFromEmail_AllowsLoginWithNewPassword()
    {
        var email = await _auth.RegisterConfirmedUserAsync();
        await _auth.ForgotPasswordAsync(email);
        var resetCode = factory.EmailSender.PasswordResetCodeSentTo(email);

        await _auth.ResetPasswordAsync(email, resetCode, NewPassword);

        var response = await _auth.LoginAsync(email, NewPassword);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidCode_ReturnsBadRequest()
    {
        var email = await _auth.RegisterConfirmedUserAsync();

        var response = await _auth.ResetPasswordAsync(email, "invalid-code", NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

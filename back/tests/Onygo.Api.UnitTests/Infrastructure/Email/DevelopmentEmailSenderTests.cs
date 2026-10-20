using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Testing;
using Onygo.Api.Infrastructure.Email;

namespace Onygo.Api.UnitTests.Infrastructure.Email;

public class DevelopmentEmailSenderTests
{
    private const string Email = "camille@onygo.test";
    private const string EncodedLink = "http://localhost/confirmEmail?userId=42&amp;code=abc";

    private readonly FakeLogger<DevelopmentEmailSender> _logger = new();
    private readonly IdentityUser _user = new() { Email = Email };

    [Fact]
    public async Task SendConfirmationLinkAsync_LogsHtmlDecodedLink()
    {
        var sender = new DevelopmentEmailSender(_logger);

        await sender.SendConfirmationLinkAsync(_user, Email, EncodedLink);

        Assert.Contains("http://localhost/confirmEmail?userId=42&code=abc", _logger.LatestRecord.Message);
    }

    [Fact]
    public async Task SendConfirmationLinkAsync_DoesNotLogEmailAddress()
    {
        var sender = new DevelopmentEmailSender(_logger);

        await sender.SendConfirmationLinkAsync(_user, Email, EncodedLink);

        Assert.DoesNotContain(Email, _logger.LatestRecord.Message);
    }

    [Fact]
    public async Task SendPasswordResetCodeAsync_DoesNotLogEmailAddress()
    {
        var sender = new DevelopmentEmailSender(_logger);

        await sender.SendPasswordResetCodeAsync(_user, Email, "reset-code");

        Assert.DoesNotContain(Email, _logger.LatestRecord.Message);
    }
}

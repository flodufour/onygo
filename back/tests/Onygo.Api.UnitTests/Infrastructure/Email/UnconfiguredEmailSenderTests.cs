using Microsoft.AspNetCore.Identity;
using Onygo.Api.Infrastructure.Email;

namespace Onygo.Api.UnitTests.Infrastructure.Email;

public class UnconfiguredEmailSenderTests
{
    [Fact]
    public async Task SendConfirmationLinkAsync_ThrowsInsteadOfDroppingTheEmail()
    {
        var sender = new UnconfiguredEmailSender();

        var send = () => sender.SendConfirmationLinkAsync(new IdentityUser(), "camille@onygo.test", "link");

        await Assert.ThrowsAsync<InvalidOperationException>(send);
    }
}

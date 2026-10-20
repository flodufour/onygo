using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Identity;

namespace Onygo.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Garde en mémoire les liens et codes envoyés, indexés par adresse e-mail.
/// </summary>
public sealed class FakeEmailSender : IEmailSender<IdentityUser>
{
    private readonly ConcurrentDictionary<string, string> _confirmationLinks = new();
    private readonly ConcurrentDictionary<string, string> _passwordResetCodes = new();

    public Task SendConfirmationLinkAsync(IdentityUser user, string email, string confirmationLink)
    {
        _confirmationLinks[email] = WebUtility.HtmlDecode(confirmationLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(IdentityUser user, string email, string resetLink) =>
        throw new NotSupportedException("The Identity API endpoints send reset codes, not links.");

    public Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode)
    {
        _passwordResetCodes[email] = WebUtility.HtmlDecode(resetCode);
        return Task.CompletedTask;
    }

    public bool HasSentConfirmationLinkTo(string email) => _confirmationLinks.ContainsKey(email);

    public string ConfirmationLinkSentTo(string email) => _confirmationLinks[email];

    public string PasswordResetCodeSentTo(string email) => _passwordResetCodes[email];
}

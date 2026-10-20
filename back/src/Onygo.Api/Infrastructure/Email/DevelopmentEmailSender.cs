using System.Net;
using Microsoft.AspNetCore.Identity;

namespace Onygo.Api.Infrastructure.Email;

/// <summary>
/// Écrit les liens et codes dans les logs au lieu d'envoyer un e-mail, pour le développement local.
/// L'adresse e-mail n'est jamais loguée (donnée personnelle).
/// </summary>
public sealed class DevelopmentEmailSender(ILogger<DevelopmentEmailSender> logger) : IEmailSender<IdentityUser>
{
    public Task SendConfirmationLinkAsync(IdentityUser user, string email, string confirmationLink)
    {
        logger.LogInformation("Email confirmation link: {ConfirmationLink}", Decode(confirmationLink));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(IdentityUser user, string email, string resetLink)
    {
        logger.LogInformation("Password reset link: {ResetLink}", Decode(resetLink));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode)
    {
        logger.LogInformation("Password reset code: {ResetCode}", Decode(resetCode));
        return Task.CompletedTask;
    }

    // Identity encode en HTML ce qu'il transmet ; on décode pour pouvoir copier le lien depuis la console.
    private static string Decode(string htmlEncoded) => WebUtility.HtmlDecode(htmlEncoded);
}

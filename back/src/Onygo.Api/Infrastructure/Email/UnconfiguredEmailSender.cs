using Microsoft.AspNetCore.Identity;

namespace Onygo.Api.Infrastructure.Email;

/// <summary>
/// Remplaçant en attendant le choix du fournisseur d'e-mails (décision ouverte, CLAUDE.md §4.3).
/// </summary>
public sealed class UnconfiguredEmailSender : IEmailSender<IdentityUser>
{
    public Task SendConfirmationLinkAsync(IdentityUser user, string email, string confirmationLink) =>
        throw NoProviderConfigured();

    public Task SendPasswordResetLinkAsync(IdentityUser user, string email, string resetLink) =>
        throw NoProviderConfigured();

    public Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode) =>
        throw NoProviderConfigured();

    private static InvalidOperationException NoProviderConfigured() =>
        new("No email provider is configured for this environment.");
}

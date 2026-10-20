using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Onygo.Api.Auth;

/// <summary>
/// Limites par adresse IP sur les routes publiques d'authentification (essais de mots de passe en masse,
/// envoi d'e-mails en rafale). Valeurs volontairement larges : tout un campus peut sortir sur Internet par la même IP.
/// </summary>
public sealed class AuthRateLimitOptions
{
    public const string SectionName = "RateLimiting:Auth";

    public int SignInPermitsPerMinute { get; init; } = 20;

    public int EmailPermitsPerHour { get; init; } = 5;
}

public static class AuthRateLimiting
{
    public const string SignInPolicy = "auth-sign-in";
    public const string EmailPolicy = "auth-email";

    // Routes de MapIdentityApi qui déclenchent l'envoi d'un e-mail : quota séparé, beaucoup plus bas.
    private static readonly string[] EmailSendingRouteSuffixes = ["/forgotPassword", "/resendConfirmationEmail"];

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthRateLimitOptions>(configuration.GetSection(AuthRateLimitOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(SignInPolicy, context =>
                FixedWindowPerIp(context, SignInPolicy, limits => limits.SignInPermitsPerMinute, TimeSpan.FromMinutes(1)));
            options.AddPolicy(EmailPolicy, context =>
                FixedWindowPerIp(context, EmailPolicy, limits => limits.EmailPermitsPerHour, TimeSpan.FromHours(1)));
        });

        return services;
    }

    public static string PolicyFor(string routePattern) =>
        EmailSendingRouteSuffixes.Any(suffix => routePattern.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            ? EmailPolicy
            : SignInPolicy;

    // Derrière le proxy de l'hébergeur, RemoteIpAddress sera celle du proxy tant que les en-têtes
    // transférés (ForwardedHeaders) ne sont pas configurés : à faire avec le choix de l'hébergement.
    private static RateLimitPartition<string> FixedWindowPerIp(
        HttpContext context,
        string policyName,
        Func<AuthRateLimitOptions, int> permitLimit,
        TimeSpan window)
    {
        var limits = context.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter($"{policyName}:{clientIp}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit(limits),
            Window = window,
            QueueLimit = 0,
        });
    }
}

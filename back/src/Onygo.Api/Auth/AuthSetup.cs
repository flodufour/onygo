using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Onygo.Api.Infrastructure.Persistence;

namespace Onygo.Api.Auth;

public static class AuthSetup
{
    private const int MinimumPasswordLength = 8;

    /// <summary>
    /// Identity avec jetons Bearer uniquement : pas de cookie, l'appli et le site n'en ont pas besoin
    /// et ce serait un second mécanisme de connexion à protéger (CSRF).
    /// </summary>
    public static IServiceCollection AddOnygoAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthRateLimiting(configuration);

        // Les jetons sont chiffrés avec ces clés : stockées en base, elles survivent aux redéploiements
        // et sont partagées entre instances. Sinon chaque redémarrage déconnecterait tout le monde.
        services.AddDataProtection()
            .PersistKeysToDbContext<OnygoDbContext>()
            .SetApplicationName("Onygo");

        services
            .AddAuthentication(IdentityConstants.BearerScheme)
            .AddBearerToken(IdentityConstants.BearerScheme);

        // Toute route exige un utilisateur connecté, sauf celles marquées explicitement AllowAnonymous.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services
            .AddIdentityCore<IdentityUser>(options =>
            {
                options.Password.RequiredLength = MinimumPasswordLength;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddEntityFrameworkStores<OnygoDbContext>()
            .AddApiEndpoints();

        return services;
    }

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // MapIdentityApi ne marque pas ses routes publiques (/login, /register…) comme anonymes :
        // on ouvre et on limite celles qui n'ont aucune exigence d'autorisation, /manage/* garde la sienne.
        app.MapGroup("/auth")
            .WithTags("Auth")
            .AddEndpointFilter<UnsupportedIdentityFeaturesFilter>()
            .MapIdentityApi<IdentityUser>()
            .Finally(OpenAndRateLimitPublicRoute);

        return app;
    }

    private static void OpenAndRateLimitPublicRoute(EndpointBuilder endpoint)
    {
        if (endpoint.Metadata.OfType<IAuthorizeData>().Any() || endpoint is not RouteEndpointBuilder route)
        {
            return;
        }

        endpoint.Metadata.Add(new AllowAnonymousAttribute());
        endpoint.Metadata.Add(new EnableRateLimitingAttribute(AuthRateLimiting.PolicyFor(route.RoutePattern.RawText ?? "")));
    }
}

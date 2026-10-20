namespace Onygo.Api.Auth;

/// <summary>
/// Ferme les options de MapIdentityApi que l'on n'utilise pas et qu'on ne peut pas retirer de la librairie :
/// la connexion par cookie (seul le Bearer est configuré) et la 2FA (hors périmètre).
/// </summary>
public sealed class UnsupportedIdentityFeaturesFilter : IEndpointFilter
{
    private const string TwoFactorPathSuffix = "/manage/2fa";
    private static readonly string[] CookieQueryParameters = ["useCookies", "useSessionCookies"];

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;

        if (request.Path.Value?.EndsWith(TwoFactorPathSuffix, StringComparison.OrdinalIgnoreCase) == true)
        {
            return ValueTask.FromResult<object?>(Results.NotFound());
        }

        if (CookieQueryParameters.Any(parameter => IsTrue(request.Query[parameter])))
        {
            return ValueTask.FromResult<object?>(Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "Cookie authentication is not supported, use bearer tokens."));
        }

        return next(context);
    }

    private static bool IsTrue(string? value) => bool.TryParse(value, out var parsed) && parsed;
}

using System.Security.Claims;

namespace Onygo.Api.Common;

public static class ClaimsPrincipalExtensions
{
    // Appelé uniquement sur des routes protégées (politique de repli) : l'identifiant est toujours présent.
    public static string UserId(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
}

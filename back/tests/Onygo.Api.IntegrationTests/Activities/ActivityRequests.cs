using Onygo.Api.Activities;
using Onygo.Api.IntegrationTests.Infrastructure;

namespace Onygo.Api.IntegrationTests.Activities;

/// <summary>
/// Demandes de création valides par défaut ; chaque test ne change que ce qui compte pour lui (`with { … }`).
/// </summary>
public static class ActivityRequests
{
    public const string LieuUniqueAddress = "Le Lieu Unique, 2 rue de la Biscuiterie, Nantes";
    public const double LieuUniqueLatitude = 47.2153;
    public const double LieuUniqueLongitude = -1.5455;

    public static CreateActivityRequest AValidActivity() => new(
        Type: ActivityType.Cafe,
        CustomType: null,
        Title: "Café au Lieu Unique",
        Hook: "Un café en terrasse avant les cours ?",
        Location: new ActivityLocationRequest(LieuUniqueAddress, LieuUniqueLatitude, LieuUniqueLongitude),
        StartsAt: OnygoApiFactory.Now.AddDays(2));
}

using System.Security.Cryptography;
using Onygo.Api.Common;

namespace Onygo.Api.Activities;

public static class ActivityRules
{
    public const int MaxTitleLength = 80;
    public const int MaxHookLength = 300;
    public const int MaxCustomTypeLength = 30;
    public const int MaxAddressLength = 200;

    public static readonly TimeSpan MaxSchedulingHorizon = TimeSpan.FromDays(30);

    public const int PublicAreaRadiusMeters = 5_000;

    // Un cercle centré sur le lieu exact révélerait l'adresse : on décale son centre.
    // Décalage toujours inférieur au rayon, pour que le lieu exact reste dans le cercle affiché.
    public const int MinPublicAreaOffsetMeters = 1_000;
    public const int MaxPublicAreaOffsetMeters = 3_000;

    /// <summary>
    /// Tire au hasard le centre du cercle public. À appeler une seule fois par activité et à stocker :
    /// recalculé à chaque affichage, la moyenne de plusieurs cercles redonnerait le lieu exact.
    /// </summary>
    public static GeoPoint DrawPublicAreaCenter(GeoPoint exactPoint)
    {
        var bearingDegrees = RandomNumberGenerator.GetInt32(0, 360);
        var offsetMeters = RandomNumberGenerator.GetInt32(MinPublicAreaOffsetMeters, MaxPublicAreaOffsetMeters + 1);
        return exactPoint.MoveBy(bearingDegrees, offsetMeters);
    }
}

namespace Onygo.Api.Common;

public sealed record GeoPoint(double Latitude, double Longitude)
{
    private const double EarthRadiusMeters = 6_371_000;

    /// <summary>Point atteint en partant d'ici sur une distance donnée, dans une direction (0° = nord, 90° = est).</summary>
    public GeoPoint MoveBy(double bearingDegrees, double distanceMeters)
    {
        var angularDistance = distanceMeters / EarthRadiusMeters;
        var bearing = ToRadians(bearingDegrees);
        var latitude = ToRadians(Latitude);
        var longitude = ToRadians(Longitude);

        var destinationLatitude = Math.Asin(
            Math.Sin(latitude) * Math.Cos(angularDistance)
            + Math.Cos(latitude) * Math.Sin(angularDistance) * Math.Cos(bearing));
        var destinationLongitude = longitude + Math.Atan2(
            Math.Sin(bearing) * Math.Sin(angularDistance) * Math.Cos(latitude),
            Math.Cos(angularDistance) - Math.Sin(latitude) * Math.Sin(destinationLatitude));

        return new GeoPoint(ToDegrees(destinationLatitude), NormalizeLongitude(ToDegrees(destinationLongitude)));
    }

    /// <summary>Distance à vol d'oiseau (formule de haversine).</summary>
    public double DistanceInMetersTo(GeoPoint other)
    {
        var deltaLatitude = ToRadians(other.Latitude - Latitude);
        var deltaLongitude = ToRadians(other.Longitude - Longitude);
        var a = Math.Pow(Math.Sin(deltaLatitude / 2), 2)
            + Math.Cos(ToRadians(Latitude)) * Math.Cos(ToRadians(other.Latitude)) * Math.Pow(Math.Sin(deltaLongitude / 2), 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Sqrt(a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private static double ToDegrees(double radians) => radians * 180 / Math.PI;

    private static double NormalizeLongitude(double longitude) => (longitude + 540) % 360 - 180;
}

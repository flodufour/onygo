using Onygo.Api.Common;

namespace Onygo.Api.UnitTests.Common;

public class GeoPointTests
{
    private static readonly GeoPoint LieuUnique = new(47.2153, -1.5455);
    private static readonly GeoPoint ChateauDesDucs = new(47.2161, -1.5497);

    [Fact]
    public void DistanceInMetersTo_TwoNantesLandmarks_ReturnsAbout330Meters()
    {
        var distance = LieuUnique.DistanceInMetersTo(ChateauDesDucs);

        Assert.InRange(distance, 320, 340);
    }

    [Fact]
    public void DistanceInMetersTo_SamePoint_ReturnsZero()
    {
        var distance = LieuUnique.DistanceInMetersTo(LieuUnique);

        Assert.Equal(0, distance, precision: 6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(200)]
    [InlineData(315)]
    public void MoveBy_ReachesPointAtRequestedDistance(double bearingDegrees)
    {
        var destination = LieuUnique.MoveBy(bearingDegrees, distanceMeters: 2_000);

        Assert.Equal(2_000, LieuUnique.DistanceInMetersTo(destination), precision: 0);
    }

    [Fact]
    public void MoveBy_TowardsNorth_IncreasesLatitudeOnly()
    {
        var destination = LieuUnique.MoveBy(bearingDegrees: 0, distanceMeters: 1_000);

        Assert.True(destination.Latitude > LieuUnique.Latitude);
        Assert.Equal(LieuUnique.Longitude, destination.Longitude, precision: 9);
    }
}

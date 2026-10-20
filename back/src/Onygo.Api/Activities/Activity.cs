using Onygo.Api.Common;

namespace Onygo.Api.Activities;

public sealed record ActivityLocation(string Address, double Latitude, double Longitude)
{
    public GeoPoint Point => new(Latitude, Longitude);
}

public class Activity
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string OrganizerId { get; init; }

    public required ActivityType Type { get; init; }

    public string? CustomType { get; init; }

    public required string Title { get; init; }

    public required string Hook { get; init; }

    /// <summary>Lieu exact : réservé à l'organisateur (et plus tard à la personne acceptée).</summary>
    public required ActivityLocation Location { get; init; }

    /// <summary>Centre du cercle montré aux autres, décalé du lieu exact une fois pour toutes à la création.</summary>
    public required GeoPoint PublicAreaCenter { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public ActivityStatus Status { get; private set; } = ActivityStatus.Open;

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>R4 : une acceptation pourvoit l'activité.</summary>
    public void MarkFilled()
    {
        if (Status != ActivityStatus.Open)
        {
            throw new InvalidOperationException($"Only an open activity can be filled (current status: {Status}).");
        }

        Status = ActivityStatus.Filled;
    }
}

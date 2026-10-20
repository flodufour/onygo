namespace Onygo.Api.Common;

/// <summary>
/// Les règles calendaires (âge, limite journalière…) se comptent en heure de Paris, pas en UTC.
/// </summary>
public static class ParisCalendar
{
    private static readonly TimeZoneInfo ParisTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), ParisTimeZone).DateTime);

    /// <summary>Début (inclus) et fin (exclue) d'une journée de Paris, en UTC.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) DayInUtc(DateOnly day) =>
        (StartOfDayInUtc(day), StartOfDayInUtc(day.AddDays(1)));

    // Minuit existe toujours à Paris : les changements d'heure ont lieu à 2 h et 3 h.
    private static DateTimeOffset StartOfDayInUtc(DateOnly day)
    {
        var localMidnight = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(localMidnight, ParisTimeZone.GetUtcOffset(localMidnight)).ToUniversalTime();
    }
}

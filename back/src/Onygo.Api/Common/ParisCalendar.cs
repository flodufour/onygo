namespace Onygo.Api.Common;

/// <summary>
/// Les règles calendaires (âge, limite journalière…) se comptent en heure de Paris, pas en UTC.
/// </summary>
public static class ParisCalendar
{
    private static readonly TimeZoneInfo ParisTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), ParisTimeZone).DateTime);
}

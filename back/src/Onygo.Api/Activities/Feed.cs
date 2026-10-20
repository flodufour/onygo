using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Onygo.Api.Activities;

/// <summary>
/// Position dans le feed : la dernière activité renvoyée. Le feed étant trié par (StartsAt, Id),
/// la page suivante commence strictement après elle, sans doublon ni trou même à date égale.
/// Transmis au front sous forme opaque : il le renvoie tel quel, sans le décoder.
/// </summary>
public sealed record FeedCursor(DateTimeOffset StartsAt, Guid ActivityId)
{
    private const char Separator = ':';

    public string Encode() =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"{StartsAt.UtcTicks}{Separator}{ActivityId:N}"));

    public static FeedCursor? TryDecode(string encoded)
    {
        if (!Base64Url.IsValid(encoded))
        {
            return null;
        }

        var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(encoded)).Split(Separator);
        if (parts.Length != 2
            || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var utcTicks)
            || utcTicks > DateTimeOffset.MaxValue.UtcTicks
            || !Guid.TryParseExact(parts[1], "N", out var activityId))
        {
            return null;
        }

        return new FeedCursor(new DateTimeOffset(utcTicks, TimeSpan.Zero), activityId);
    }
}

public sealed record FeedQuery(FeedCursor? After, int PageSize);

public static class FeedRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    /// <summary>Lit les paramètres de la requête ; renvoie la requête valide, ou les erreurs par champ.</summary>
    public static (FeedQuery? Query, Dictionary<string, string[]> Errors) Parse(string? cursor, int? limit)
    {
        var errors = new Dictionary<string, string[]>();
        var pageSize = limit ?? DefaultPageSize;
        if (pageSize is < 1 or > MaxPageSize)
        {
            errors["limit"] = [ActivityErrorCodes.LimitOutOfRange];
        }

        var after = cursor is null ? null : FeedCursor.TryDecode(cursor);
        if (cursor is not null && after is null)
        {
            errors["cursor"] = [ActivityErrorCodes.InvalidCursor];
        }

        return errors.Count > 0 ? (null, errors) : (new FeedQuery(after, pageSize), errors);
    }
}

using Onygo.Api.Activities;

namespace Onygo.Api.UnitTests.Activities;

public class FeedTests
{
    private static readonly FeedCursor ACursor =
        new(new DateTimeOffset(2026, 6, 17, 18, 30, 0, TimeSpan.Zero), Guid.Parse("0197a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b"));

    [Fact]
    public void FeedCursor_EncodedThenDecoded_GivesBackTheSamePosition()
    {
        var decoded = FeedCursor.TryDecode(ACursor.Encode());

        Assert.Equal(ACursor, decoded);
    }

    [Fact]
    public void FeedCursor_Encoded_IsUrlSafe()
    {
        var encoded = ACursor.Encode();

        Assert.Equal(Uri.EscapeDataString(encoded), encoded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a cursor!")]
    [InlineData("MTIz")] // « 123 » : pas d'identifiant
    [InlineData("OTk5OTk5OTk5OTk5OTk5OTk5OTk6MDE5N2ExYjJjM2Q0N2U1ZjhhOWIwYzFkMmUzZjRhNWI")] // date hors limites
    public void FeedCursor_TryDecodeInvalidValue_ReturnsNull(string encoded)
    {
        var decoded = FeedCursor.TryDecode(encoded);

        Assert.Null(decoded);
    }

    [Fact]
    public void Parse_WithoutParameters_StartsFromTheBeginningWithDefaultPageSize()
    {
        var (query, _) = FeedRules.Parse(cursor: null, limit: null);

        Assert.Equal(new FeedQuery(After: null, FeedRules.DefaultPageSize), query);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(FeedRules.MaxPageSize + 1)]
    public void Parse_LimitOutOfRange_ReturnsLimitOutOfRange(int limit)
    {
        var (query, errors) = FeedRules.Parse(cursor: null, limit);

        Assert.Null(query);
        Assert.Equal([ActivityErrorCodes.LimitOutOfRange], errors["limit"]);
    }

    [Fact]
    public void Parse_InvalidCursor_ReturnsInvalidCursor()
    {
        var (query, errors) = FeedRules.Parse(cursor: "not a cursor!", limit: null);

        Assert.Null(query);
        Assert.Equal([ActivityErrorCodes.InvalidCursor], errors["cursor"]);
    }

    [Fact]
    public void Parse_ValidCursor_ResumesAfterIt()
    {
        var (query, _) = FeedRules.Parse(ACursor.Encode(), limit: 10);

        Assert.Equal(new FeedQuery(ACursor, 10), query);
    }
}

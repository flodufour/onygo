using Onygo.Api.Activities;

namespace Onygo.Api.UnitTests.Activities;

public class ActivityRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsShortNotice_StartingIn5Hours59_ReturnsTrue()
    {
        var isShortNotice = ActivityRules.IsShortNotice(startsAt: Now.AddHours(6).AddMinutes(-1), now: Now);

        Assert.True(isShortNotice);
    }

    [Fact]
    public void IsShortNotice_StartingInExactly6Hours_ReturnsFalse()
    {
        var isShortNotice = ActivityRules.IsShortNotice(startsAt: Now.AddHours(6), now: Now);

        Assert.False(isShortNotice);
    }

    [Fact]
    public void AreApplicationsClosed_Exactly1HourBeforeStart_ReturnsTrue()
    {
        var areClosed = ActivityRules.AreApplicationsClosed(startsAt: Now.AddHours(1), now: Now);

        Assert.True(areClosed);
    }

    [Fact]
    public void AreApplicationsClosed_1HourAnd1MinuteBeforeStart_ReturnsFalse()
    {
        var areClosed = ActivityRules.AreApplicationsClosed(startsAt: Now.AddHours(1).AddMinutes(1), now: Now);

        Assert.False(areClosed);
    }
}

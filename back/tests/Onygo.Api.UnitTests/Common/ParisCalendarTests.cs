using Microsoft.Extensions.Time.Testing;
using Onygo.Api.Common;

namespace Onygo.Api.UnitTests.Common;

public class ParisCalendarTests
{
    [Fact]
    public void Today_LateEveningUtc_ReturnsNextDayAlreadyStartedInParis()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 14, 22, 30, 0, TimeSpan.Zero));

        var today = ParisCalendar.Today(clock);

        Assert.Equal(new DateOnly(2026, 6, 15), today);
    }

    [Fact]
    public void Today_JustBeforeMidnightInParisInWinter_ReturnsSameDay()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 10, 22, 59, 0, TimeSpan.Zero));

        var today = ParisCalendar.Today(clock);

        Assert.Equal(new DateOnly(2026, 1, 10), today);
    }
}

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

    [Fact]
    public void DayInUtc_SummerDay_StartsAt22hUtcTheDayBefore()
    {
        var (start, end) = ParisCalendar.DayInUtc(new DateOnly(2026, 6, 15));

        Assert.Equal(new DateTimeOffset(2026, 6, 14, 22, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 6, 15, 22, 0, 0, TimeSpan.Zero), end);
    }

    [Fact]
    public void DayInUtc_DaylightSavingStartDay_Lasts23Hours()
    {
        var (start, end) = ParisCalendar.DayInUtc(new DateOnly(2026, 3, 29));

        Assert.Equal(new DateTimeOffset(2026, 3, 28, 23, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(TimeSpan.FromHours(23), end - start);
    }
}

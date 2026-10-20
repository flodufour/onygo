using Onygo.Api.Profiles;

namespace Onygo.Api.UnitTests.Profiles;

public class ProfileRulesTests
{
    private static readonly DateOnly BirthDate = new(2008, 6, 15);

    [Fact]
    public void AgeOn_DayBeforeBirthday_ReturnsPreviousAge()
    {
        var age = ProfileRules.AgeOn(BirthDate, today: new DateOnly(2026, 6, 14));

        Assert.Equal(17, age);
    }

    [Fact]
    public void AgeOn_Birthday_ReturnsNewAge()
    {
        var age = ProfileRules.AgeOn(BirthDate, today: new DateOnly(2026, 6, 15));

        Assert.Equal(18, age);
    }

    // Né un 29 février : les années non bissextiles, l'anniversaire est compté le 1er mars.
    [Fact]
    public void AgeOn_February28OfNonLeapYear_ForLeapDayBirth_ReturnsPreviousAge()
    {
        var age = ProfileRules.AgeOn(new DateOnly(2008, 2, 29), today: new DateOnly(2026, 2, 28));

        Assert.Equal(17, age);
    }

    [Fact]
    public void AgeOn_March1OfNonLeapYear_ForLeapDayBirth_ReturnsNewAge()
    {
        var age = ProfileRules.AgeOn(new DateOnly(2008, 2, 29), today: new DateOnly(2026, 3, 1));

        Assert.Equal(18, age);
    }

    [Fact]
    public void IsAdult_On18thBirthday_ReturnsTrue()
    {
        var isAdult = ProfileRules.IsAdult(BirthDate, today: new DateOnly(2026, 6, 15));

        Assert.True(isAdult);
    }

    [Fact]
    public void IsAdult_DayBefore18thBirthday_ReturnsFalse()
    {
        var isAdult = ProfileRules.IsAdult(BirthDate, today: new DateOnly(2026, 6, 14));

        Assert.False(isAdult);
    }
}

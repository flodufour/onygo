using Onygo.Api.Activities;

namespace Onygo.Api.UnitTests.Activities;

public class ActivityValidationTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 8, 0, 0, TimeSpan.Zero);

    private static CreateActivityRequest AValidActivity() => new(
        Type: ActivityType.Bar,
        CustomType: null,
        Title: "Apéro quai de la Fosse",
        Hook: "Une bière au soleil ?",
        Location: new ActivityLocationRequest("Quai de la Fosse, Nantes", 47.2110, -1.5640),
        StartsAt: Now.AddHours(5));

    [Fact]
    public void ValidateCreation_WithValidRequest_ReturnsNoError()
    {
        var errors = ActivityValidation.ValidateCreation(AValidActivity(), Now);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateCreation_WithoutType_ReturnsTypeRequired()
    {
        var errors = ActivityValidation.ValidateCreation(AValidActivity() with { Type = null }, Now);

        Assert.Equal([ActivityErrorCodes.TypeRequired], errors["type"]);
    }

    [Fact]
    public void ValidateCreation_CustomTypeWithPredefinedType_ReturnsCustomTypeNotAllowed()
    {
        var errors = ActivityValidation.ValidateCreation(AValidActivity() with { CustomType = "Pétanque" }, Now);

        Assert.Equal([ActivityErrorCodes.CustomTypeNotAllowed], errors["customType"]);
    }

    [Fact]
    public void ValidateCreation_TooLongCustomType_ReturnsCustomTypeTooLong()
    {
        var request = AValidActivity() with
        {
            Type = ActivityType.Other,
            CustomType = new string('a', ActivityRules.MaxCustomTypeLength + 1),
        };

        var errors = ActivityValidation.ValidateCreation(request, Now);

        Assert.Equal([ActivityErrorCodes.CustomTypeTooLong], errors["customType"]);
    }

    [Fact]
    public void ValidateCreation_BlankTitle_ReturnsTitleRequired()
    {
        var errors = ActivityValidation.ValidateCreation(AValidActivity() with { Title = "  " }, Now);

        Assert.Equal([ActivityErrorCodes.TitleRequired], errors["title"]);
    }

    [Fact]
    public void ValidateCreation_TooLongHook_ReturnsHookTooLong()
    {
        var request = AValidActivity() with { Hook = new string('a', ActivityRules.MaxHookLength + 1) };

        var errors = ActivityValidation.ValidateCreation(request, Now);

        Assert.Equal([ActivityErrorCodes.HookTooLong], errors["hook"]);
    }

    [Fact]
    public void ValidateCreation_WithoutLocation_ReturnsLocationRequired()
    {
        var errors = ActivityValidation.ValidateCreation(AValidActivity() with { Location = null }, Now);

        Assert.Equal([ActivityErrorCodes.LocationRequired], errors["location"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-90.1)]
    [InlineData(90.1)]
    public void ValidateCreation_InvalidLatitude_ReturnsLatitudeInvalid(double? latitude)
    {
        var request = AValidActivity() with
        {
            Location = new ActivityLocationRequest("Quai de la Fosse, Nantes", latitude, -1.5640),
        };

        var errors = ActivityValidation.ValidateCreation(request, Now);

        Assert.Equal([ActivityErrorCodes.LatitudeInvalid], errors["location.latitude"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-180.1)]
    [InlineData(180.1)]
    public void ValidateCreation_InvalidLongitude_ReturnsLongitudeInvalid(double? longitude)
    {
        var request = AValidActivity() with
        {
            Location = new ActivityLocationRequest("Quai de la Fosse, Nantes", 47.2110, longitude),
        };

        var errors = ActivityValidation.ValidateCreation(request, Now);

        Assert.Equal([ActivityErrorCodes.LongitudeInvalid], errors["location.longitude"]);
    }

    [Fact]
    public void ValidateCreation_BlankAddress_ReturnsAddressRequired()
    {
        var request = AValidActivity() with { Location = new ActivityLocationRequest(" ", 47.2110, -1.5640) };

        var errors = ActivityValidation.ValidateCreation(request, Now);

        Assert.Equal([ActivityErrorCodes.AddressRequired], errors["location.address"]);
    }

    [Fact]
    public void ValidateCreation_StartingNow_ReturnsStartsAtInPast()
    {
        var errors = ActivityValidation.ValidateCreation(AValidActivity() with { StartsAt = Now }, Now);

        Assert.Equal([ActivityErrorCodes.StartsAtInPast], errors["startsAt"]);
    }

    // Personne ne pourrait postuler : les candidatures ferment 2 h avant le début (R5).
    [Fact]
    public void ValidateCreation_StartingIn2Hours_ReturnsStartsAtTooSoon()
    {
        var errors = ActivityValidation.ValidateCreation(AValidActivity() with { StartsAt = Now.AddHours(2) }, Now);

        Assert.Equal([ActivityErrorCodes.StartsAtTooSoon], errors["startsAt"]);
    }

    [Fact]
    public void ValidateCreation_StartingIn2HoursAnd1Minute_ReturnsNoError()
    {
        var request = AValidActivity() with { StartsAt = Now.AddHours(2).AddMinutes(1) };

        var errors = ActivityValidation.ValidateCreation(request, Now);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateCreation_WithoutStartsAt_ReturnsStartsAtRequired()
    {
        var errors = ActivityValidation.ValidateCreation(AValidActivity() with { StartsAt = null }, Now);

        Assert.Equal([ActivityErrorCodes.StartsAtRequired], errors["startsAt"]);
    }
}

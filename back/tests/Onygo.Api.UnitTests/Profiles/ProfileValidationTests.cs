using Onygo.Api.Profiles;

namespace Onygo.Api.UnitTests.Profiles;

public class ProfileValidationTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);
    private static readonly DateOnly AdultBirthDate = new(2000, 1, 1);

    [Fact]
    public void ValidateCreation_WithValidRequest_ReturnsNoError()
    {
        var errors = ProfileValidation.ValidateCreation(new CreateProfileRequest("Camille", AdultBirthDate), Today);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateCreation_WithUnderageBirthDate_ReturnsUnderage()
    {
        var request = new CreateProfileRequest("Camille", new DateOnly(2008, 6, 16));

        var errors = ProfileValidation.ValidateCreation(request, Today);

        Assert.Equal([ProfileErrorCodes.Underage], errors["birthDate"]);
    }

    [Fact]
    public void ValidateCreation_WithoutBirthDate_ReturnsBirthDateRequired()
    {
        var errors = ProfileValidation.ValidateCreation(new CreateProfileRequest("Camille", null), Today);

        Assert.Equal([ProfileErrorCodes.BirthDateRequired], errors["birthDate"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateCreation_WithBlankFirstName_ReturnsFirstNameRequired(string? firstName)
    {
        var errors = ProfileValidation.ValidateCreation(new CreateProfileRequest(firstName, AdultBirthDate), Today);

        Assert.Equal([ProfileErrorCodes.FirstNameRequired], errors["firstName"]);
    }

    [Fact]
    public void ValidateCreation_WithTooLongFirstName_ReturnsFirstNameTooLong()
    {
        var firstName = new string('a', ProfileRules.MaxFirstNameLength + 1);

        var errors = ProfileValidation.ValidateCreation(new CreateProfileRequest(firstName, AdultBirthDate), Today);

        Assert.Equal([ProfileErrorCodes.FirstNameTooLong], errors["firstName"]);
    }

    [Fact]
    public void ValidateUpdate_WithBirthDate_ReturnsBirthDateNotEditable()
    {
        var errors = ProfileValidation.ValidateUpdate(new UpdateProfileRequest("Camille", AdultBirthDate));

        Assert.Equal([ProfileErrorCodes.BirthDateNotEditable], errors["birthDate"]);
    }

    [Fact]
    public void ValidateUpdate_WithoutAnyField_ReturnsNoError()
    {
        var errors = ProfileValidation.ValidateUpdate(new UpdateProfileRequest(null, null));

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateUpdate_WithBlankFirstName_ReturnsFirstNameRequired()
    {
        var errors = ProfileValidation.ValidateUpdate(new UpdateProfileRequest("  ", null));

        Assert.Equal([ProfileErrorCodes.FirstNameRequired], errors["firstName"]);
    }
}

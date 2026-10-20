using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Onygo.Api.IntegrationTests.Infrastructure;
using Onygo.Api.Profiles;

namespace Onygo.Api.IntegrationTests.Profiles;

[Collection(ApiCollection.Name)]
public class ProfileTests(OnygoApiFactory factory)
{
    private static readonly DateOnly AdultBirthDate = OnygoApiFactory.Today.AddYears(-25);

    private readonly AuthApiClient _auth = new(factory);

    [Fact]
    public async Task CreateProfile_WithValidData_ReturnsCreatedProfileWithAge()
    {
        var client = await _auth.CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("/profile", new { firstName = "Camille", birthDate = AdultBirthDate });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.Equal(new ProfileResponse("Camille", AdultBirthDate, 25), profile);
    }

    [Fact]
    public async Task CreateProfile_TrimsFirstName()
    {
        var client = await _auth.CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("/profile", new { firstName = "  Camille ", birthDate = AdultBirthDate });

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.Equal("Camille", profile!.FirstName);
    }

    [Fact]
    public async Task CreateProfile_WhenUserTurns18Today_ReturnsCreated()
    {
        var client = await _auth.CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync(
            "/profile",
            new { firstName = "Camille", birthDate = OnygoApiFactory.Today.AddYears(-18) });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateProfile_WhenUserTurns18Tomorrow_ReturnsUnderage()
    {
        var client = await _auth.CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync(
            "/profile",
            new { firstName = "Camille", birthDate = OnygoApiFactory.Today.AddYears(-18).AddDays(1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal([ProfileErrorCodes.Underage], problem!.Errors["birthDate"]);
    }

    [Fact]
    public async Task CreateProfile_WithBlankFirstName_ReturnsFirstNameRequired()
    {
        var client = await _auth.CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("/profile", new { firstName = " ", birthDate = AdultBirthDate });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal([ProfileErrorCodes.FirstNameRequired], problem!.Errors["firstName"]);
    }

    [Fact]
    public async Task CreateProfile_WhenProfileAlreadyExists_ReturnsConflict()
    {
        var client = await _auth.CreateSignedInClientAsync();
        await client.PostAsJsonAsync("/profile", new { firstName = "Camille", birthDate = AdultBirthDate });

        var response = await client.PostAsJsonAsync("/profile", new { firstName = "Léa", birthDate = AdultBirthDate });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateProfile_WithoutAccessToken_ReturnsUnauthorized()
    {
        var anonymousClient = factory.CreateClient();

        var response = await anonymousClient.PostAsJsonAsync(
            "/profile",
            new { firstName = "Camille", birthDate = AdultBirthDate });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_WhenNoProfileYet_ReturnsNotFound()
    {
        var client = await _auth.CreateSignedInClientAsync();

        var response = await client.GetAsync("/profile");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_ReturnsOnlyTheSignedInUserProfile()
    {
        var camille = await _auth.CreateSignedInClientAsync();
        var lea = await _auth.CreateSignedInClientAsync();
        await camille.PostAsJsonAsync("/profile", new { firstName = "Camille", birthDate = AdultBirthDate });
        await lea.PostAsJsonAsync("/profile", new { firstName = "Léa", birthDate = AdultBirthDate });

        var profile = await camille.GetFromJsonAsync<ProfileResponse>("/profile");

        Assert.Equal("Camille", profile!.FirstName);
    }

    [Fact]
    public async Task UpdateProfile_WithNewFirstName_SavesIt()
    {
        var client = await _auth.CreateSignedInClientAsync();
        await client.PostAsJsonAsync("/profile", new { firstName = "Camille", birthDate = AdultBirthDate });

        await client.PatchAsJsonAsync("/profile", new { firstName = "Cam" });

        var profile = await client.GetFromJsonAsync<ProfileResponse>("/profile");
        Assert.Equal("Cam", profile!.FirstName);
    }

    [Fact]
    public async Task UpdateProfile_WithBirthDate_ReturnsBirthDateNotEditableAndKeepsIt()
    {
        var client = await _auth.CreateSignedInClientAsync();
        await client.PostAsJsonAsync("/profile", new { firstName = "Camille", birthDate = AdultBirthDate });

        var response = await client.PatchAsJsonAsync("/profile", new { birthDate = AdultBirthDate.AddYears(-1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal([ProfileErrorCodes.BirthDateNotEditable], problem!.Errors["birthDate"]);
        var profile = await client.GetFromJsonAsync<ProfileResponse>("/profile");
        Assert.Equal(AdultBirthDate, profile!.BirthDate);
    }

    [Fact]
    public async Task UpdateProfile_WithBlankFirstName_ReturnsFirstNameRequired()
    {
        var client = await _auth.CreateSignedInClientAsync();
        await client.PostAsJsonAsync("/profile", new { firstName = "Camille", birthDate = AdultBirthDate });

        var response = await client.PatchAsJsonAsync("/profile", new { firstName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_WhenNoProfileYet_ReturnsNotFound()
    {
        var client = await _auth.CreateSignedInClientAsync();

        var response = await client.PatchAsJsonAsync("/profile", new { firstName = "Cam" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

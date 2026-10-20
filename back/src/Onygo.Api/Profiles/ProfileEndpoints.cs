using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Onygo.Api.Common;
using Onygo.Api.Infrastructure.Persistence;

namespace Onygo.Api.Profiles;

/// <summary>
/// Profil de l'utilisateur connecté. Pas de route publique : la politique de repli exige un jeton.
/// </summary>
public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var profile = app.MapGroup("/profile").WithTags("Profile");
        profile.MapGet("/", GetProfile);
        profile.MapPost("/", CreateProfile);
        profile.MapPatch("/", UpdateProfile);
        return app;
    }

    private static async Task<Results<Ok<ProfileResponse>, NotFound>> GetProfile(
        ClaimsPrincipal user,
        OnygoDbContext dbContext,
        TimeProvider clock)
    {
        var profile = await dbContext.Profiles.FindAsync(UserIdOf(user));
        return profile is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(profile, clock));
    }

    private static async Task<Results<Created<ProfileResponse>, ValidationProblem, Conflict>> CreateProfile(
        CreateProfileRequest request,
        ClaimsPrincipal user,
        OnygoDbContext dbContext,
        TimeProvider clock)
    {
        var errors = ProfileValidation.ValidateCreation(request, ParisCalendar.Today(clock));
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (await dbContext.Profiles.FindAsync(UserIdOf(user)) is not null)
        {
            return TypedResults.Conflict();
        }

        var profile = new Profile(UserIdOf(user), request.FirstName!.Trim(), request.BirthDate!.Value);
        dbContext.Profiles.Add(profile);
        await dbContext.SaveChangesAsync();

        return TypedResults.Created("/profile", ToResponse(profile, clock));
    }

    private static async Task<Results<Ok<ProfileResponse>, ValidationProblem, NotFound>> UpdateProfile(
        UpdateProfileRequest request,
        ClaimsPrincipal user,
        OnygoDbContext dbContext,
        TimeProvider clock)
    {
        var errors = ProfileValidation.ValidateUpdate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var profile = await dbContext.Profiles.FindAsync(UserIdOf(user));
        if (profile is null)
        {
            return TypedResults.NotFound();
        }

        if (request.FirstName is not null)
        {
            profile.ChangeFirstName(request.FirstName.Trim());
        }

        await dbContext.SaveChangesAsync();

        return TypedResults.Ok(ToResponse(profile, clock));
    }

    private static string UserIdOf(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private static ProfileResponse ToResponse(Profile profile, TimeProvider clock) =>
        new(profile.FirstName, profile.BirthDate, ProfileRules.AgeOn(profile.BirthDate, ParisCalendar.Today(clock)));
}

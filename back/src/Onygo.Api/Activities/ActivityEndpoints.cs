using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Onygo.Api.Common;
using Onygo.Api.Infrastructure.Persistence;
using Onygo.Api.Profiles;

namespace Onygo.Api.Activities;

public static class ActivityEndpoints
{
    private sealed record ActivityWithOrganizer(Activity Activity, Profile Organizer);

    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        var activities = app.MapGroup("/activities").WithTags("Activities");
        activities.MapPost("/", CreateActivity);
        activities.MapGet("/{id:guid}", GetActivity);
        return app;
    }

    private static async Task<Results<Created<ActivityResponse>, ValidationProblem, ProblemHttpResult>> CreateActivity(
        CreateActivityRequest request,
        ClaimsPrincipal user,
        OnygoDbContext dbContext,
        TimeProvider clock)
    {
        // La carte affiche le prénom et l'âge de l'organisateur, et le profil garantit ses 18 ans.
        var organizer = await dbContext.Profiles.FindAsync(user.UserId());
        if (organizer is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "A profile is required to create an activity.",
                extensions: new Dictionary<string, object?> { ["code"] = ActivityErrorCodes.ProfileRequired });
        }

        var errors = ActivityValidation.ValidateCreation(request, clock.GetUtcNow());
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var activity = NewActivity(request, organizer.UserId, clock);
        dbContext.Activities.Add(activity);
        await dbContext.SaveChangesAsync();

        var response = ToResponse(new ActivityWithOrganizer(activity, organizer), organizer.UserId, clock);
        return TypedResults.Created($"/activities/{activity.Id}", response);
    }

    private static async Task<Results<Ok<ActivityResponse>, NotFound>> GetActivity(
        Guid id,
        ClaimsPrincipal user,
        OnygoDbContext dbContext,
        TimeProvider clock)
    {
        var found = await (
            from activity in dbContext.Activities
            join organizer in dbContext.Profiles on activity.OrganizerId equals organizer.UserId
            where activity.Id == id
            select new ActivityWithOrganizer(activity, organizer)).SingleOrDefaultAsync();

        return found is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(found, user.UserId(), clock));
    }

    // Appelé après validation : les champs obligatoires sont présents.
    private static Activity NewActivity(CreateActivityRequest request, string organizerId, TimeProvider clock)
    {
        var location = new ActivityLocation(
            request.Location!.Address!.Trim(),
            request.Location.Latitude!.Value,
            request.Location.Longitude!.Value);
        return new Activity
        {
            OrganizerId = organizerId,
            Type = request.Type!.Value,
            CustomType = request.Type == ActivityType.Other ? request.CustomType!.Trim() : null,
            Title = request.Title!.Trim(),
            Hook = request.Hook!.Trim(),
            Location = location,
            PublicAreaCenter = ActivityRules.DrawPublicAreaCenter(location.Point),
            // PostgreSQL (timestamptz) n'accepte que des dates en UTC.
            StartsAt = request.StartsAt!.Value.ToUniversalTime(),
            CreatedAt = clock.GetUtcNow(),
        };
    }

    private static ActivityResponse ToResponse(ActivityWithOrganizer found, string viewerId, TimeProvider clock)
    {
        var (activity, organizer) = found;
        return new ActivityResponse(
            activity.Id,
            activity.Type,
            activity.CustomType,
            activity.Title,
            activity.Hook,
            activity.StartsAt,
            activity.Status,
            new PublicAreaResponse(
                activity.PublicAreaCenter.Latitude,
                activity.PublicAreaCenter.Longitude,
                ActivityRules.PublicAreaRadiusMeters),
            CanSeeExactLocation(activity, viewerId) ? ToExactLocation(activity.Location) : null,
            new OrganizerResponse(
                organizer.FirstName,
                ProfileRules.AgeOn(organizer.BirthDate, ParisCalendar.Today(clock))));
    }

    // Pour l'instant seul l'organisateur ; la personne acceptée y aura droit avec les candidatures.
    private static bool CanSeeExactLocation(Activity activity, string viewerId) => activity.OrganizerId == viewerId;

    private static ExactLocationResponse ToExactLocation(ActivityLocation location) =>
        new(location.Address, location.Latitude, location.Longitude);
}

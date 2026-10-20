using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Onygo.Api.Applications;
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
        activities.MapGet("/feed", GetFeed);
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
            return BusinessProblem.Forbidden(ActivityErrorCodes.ProfileRequired);
        }

        var errors = ActivityValidation.ValidateCreation(request, clock.GetUtcNow());
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var activity = NewActivity(request, organizer.UserId, clock);
        dbContext.Activities.Add(activity);
        await dbContext.SaveChangesAsync();

        var response = ToResponse(new ActivityWithOrganizer(activity, organizer), canSeeExactLocation: true, clock);
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

        if (found is null)
        {
            return TypedResults.NotFound();
        }

        var canSeeExactLocation = await CanSeeExactLocationAsync(dbContext, found.Activity, user.UserId());
        return TypedResults.Ok(ToResponse(found, canSeeExactLocation, clock));
    }

    /// <summary>
    /// Activités proposables à l'utilisateur (CLAUDE.md racine §2.5), de la plus proche à la plus lointaine.
    /// </summary>
    private static async Task<Results<Ok<FeedPageResponse>, ValidationProblem>> GetFeed(
        string? cursor,
        int? limit,
        ClaimsPrincipal user,
        OnygoDbContext dbContext,
        TimeProvider clock)
    {
        var (query, errors) = FeedRules.Parse(cursor, limit);
        if (query is null)
        {
            return TypedResults.ValidationProblem(errors);
        }

        // Une ligne de plus que la page : sa présence dit s'il reste des activités après.
        var rows = await FeedActivities(dbContext, user.UserId(), clock.GetUtcNow(), query.After)
            .Take(query.PageSize + 1)
            .ToListAsync();
        var page = rows.Take(query.PageSize).ToList();
        var nextCursor = rows.Count > query.PageSize
            ? new FeedCursor(page[^1].Activity.StartsAt, page[^1].Activity.Id).Encode()
            : null;

        // Le feed ne contient que des activités d'autres organisateurs, sans candidature de l'utilisateur :
        // il n'a donc jamais droit au lieu exact.
        var activities = page.Select(found => ToResponse(found, canSeeExactLocation: false, clock)).ToList();
        return TypedResults.Ok(new FeedPageResponse(activities, nextCursor));
    }

    private static IQueryable<ActivityWithOrganizer> FeedActivities(
        OnygoDbContext dbContext,
        string viewerId,
        DateTimeOffset now,
        FeedCursor? after)
    {
        var candidates =
            from activity in dbContext.Activities
            join organizer in dbContext.Profiles on activity.OrganizerId equals organizer.UserId
            where activity.Status == ActivityStatus.Open
                && activity.StartsAt > now
                && activity.OrganizerId != viewerId
                // R3 : une candidature, quel que soit son statut, retire l'activité du feed (un refusé ne la revoit pas).
                && !dbContext.Applications.Any(application =>
                    application.ActivityId == activity.Id && application.ApplicantId == viewerId)
                // R1 : avec 3 candidatures en attente, l'activité disparaît ; un refus la fait revenir (R2).
                && dbContext.Applications.Count(application =>
                    application.ActivityId == activity.Id && application.Status == ApplicationStatus.Pending)
                    < ApplicationRules.MaxPendingApplicationsPerActivity
            select new { Activity = activity, Organizer = organizer };

        if (after is not null)
        {
            candidates = candidates.Where(found =>
                found.Activity.StartsAt > after.StartsAt
                || (found.Activity.StartsAt == after.StartsAt && found.Activity.Id.CompareTo(after.ActivityId) > 0));
        }

        return candidates
            .OrderBy(found => found.Activity.StartsAt)
            .ThenBy(found => found.Activity.Id)
            .Select(found => new ActivityWithOrganizer(found.Activity, found.Organizer));
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

    private static ActivityResponse ToResponse(ActivityWithOrganizer found, bool canSeeExactLocation, TimeProvider clock)
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
            canSeeExactLocation ? ToExactLocation(activity.Location) : null,
            new OrganizerResponse(
                organizer.FirstName,
                ProfileRules.AgeOn(organizer.BirthDate, ParisCalendar.Today(clock))),
            ActivityRules.IsShortNotice(activity.StartsAt, clock.GetUtcNow()));
    }

    /// <summary>Le lieu exact n'est connu que de l'organisateur et, après le match, de la personne acceptée.</summary>
    private static async Task<bool> CanSeeExactLocationAsync(OnygoDbContext dbContext, Activity activity, string viewerId) =>
        activity.OrganizerId == viewerId
        || await dbContext.Applications.AnyAsync(application =>
            application.ActivityId == activity.Id
            && application.ApplicantId == viewerId
            && application.Status == ApplicationStatus.Accepted);

    private static ExactLocationResponse ToExactLocation(ActivityLocation location) =>
        new(location.Address, location.Latitude, location.Longitude);
}

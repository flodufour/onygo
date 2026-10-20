using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Onygo.Api.Activities;
using Onygo.Api.Common;
using Onygo.Api.Infrastructure.Persistence;
using Onygo.Api.Profiles;

namespace Onygo.Api.Applications;

/// <summary>
/// Postuler (O), trier les candidatures (O / X) et suivre les siennes.
/// R1, R4 et R6 dépendent de comptages : on les fait dans une transaction, sous verrou de ligne (FOR UPDATE),
/// pour que deux requêtes simultanées ne puissent pas les contourner.
/// </summary>
public static class ApplicationEndpoints
{
    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var activityApplications = app.MapGroup("/activities/{activityId:guid}/applications").WithTags("Applications");
        activityApplications.MapPost("/", Apply);
        activityApplications.MapGet("/", ListReceivedApplications);

        var applications = app.MapGroup("/applications").WithTags("Applications");
        applications.MapGet("/mine", ListMyApplications);
        applications.MapPost("/{id:guid}/accept", Accept);
        applications.MapPost("/{id:guid}/refuse", Refuse);
        return app;
    }

    private static async Task<Results<Created<MyApplicationResponse>, ValidationProblem, NotFound, ProblemHttpResult>> Apply(
        Guid activityId,
        ApplyRequest request,
        ClaimsPrincipal user,
        OnygoDbContext dbContext,
        TimeProvider clock)
    {
        var applicantId = user.UserId();
        // L'organisateur trie les candidats sur leur prénom et leur âge, et le profil garantit leurs 18 ans.
        if (!await dbContext.Profiles.AnyAsync(profile => profile.UserId == applicantId))
        {
            return BusinessProblem.Forbidden(ApplicationErrorCodes.ProfileRequired);
        }

        var message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim();
        if (message?.Length > ApplicationRules.MaxMessageLength)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["message"] = [ApplicationErrorCodes.MessageTooLong],
            });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        await LockUserAsync(dbContext, applicantId);
        var activity = await LockActivityAsync(dbContext, activityId);
        if (activity is null)
        {
            return TypedResults.NotFound();
        }

        var now = clock.GetUtcNow();
        var error = ApplicationRules.CheckCanApply(await ReadApplyCheckAsync(dbContext, activity, applicantId, clock));
        if (error is not null)
        {
            return ProblemFor(error);
        }

        var application = new ActivityApplication
        {
            ActivityId = activity.Id,
            ApplicantId = applicantId,
            Message = message,
            AppliedAt = now,
        };
        dbContext.Applications.Add(application);
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        return TypedResults.Created((string?)null, ToMyApplication(application, activity, now));
    }

    private static async Task<Results<Ok<List<ReceivedApplicationResponse>>, NotFound, ProblemHttpResult>> ListReceivedApplications(
        Guid activityId,
        ClaimsPrincipal user,
        OnygoDbContext dbContext,
        TimeProvider clock)
    {
        var activity = await dbContext.Activities.FindAsync(activityId);
        if (activity is null)
        {
            return TypedResults.NotFound();
        }

        if (activity.OrganizerId != user.UserId())
        {
            return BusinessProblem.Forbidden(ApplicationErrorCodes.NotOrganizer);
        }

        var pending = await (
            from application in dbContext.Applications
            join applicant in dbContext.Profiles on application.ApplicantId equals applicant.UserId
            where application.ActivityId == activityId && application.Status == ApplicationStatus.Pending
            orderby application.AppliedAt
            select new { application, applicant }).ToListAsync();

        var today = ParisCalendar.Today(clock);
        return TypedResults.Ok(pending
            .Select(row => new ReceivedApplicationResponse(
                row.application.Id,
                row.applicant.FirstName,
                ProfileRules.AgeOn(row.applicant.BirthDate, today),
                row.application.Message,
                row.application.AppliedAt))
            .ToList());
    }

    private static async Task<Ok<List<MyApplicationResponse>>> ListMyApplications(
        ClaimsPrincipal user,
        OnygoDbContext dbContext,
        TimeProvider clock)
    {
        var applicantId = user.UserId();
        // R9 : les candidatures refusées ou expirées disparaissent, sans que le candidat sache pourquoi.
        var visible = await (
            from application in dbContext.Applications
            join activity in dbContext.Activities on application.ActivityId equals activity.Id
            where application.ApplicantId == applicantId
                && (application.Status == ApplicationStatus.Pending || application.Status == ApplicationStatus.Accepted)
            orderby activity.StartsAt
            select new { application, activity }).ToListAsync();

        var now = clock.GetUtcNow();
        return TypedResults.Ok(visible.Select(row => ToMyApplication(row.application, row.activity, now)).ToList());
    }

    private static async Task<Results<Ok<AcceptedApplicationResponse>, NotFound, ProblemHttpResult>> Accept(
        Guid id,
        ClaimsPrincipal user,
        OnygoDbContext dbContext,
        TimeProvider clock)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var loaded = await LoadForDecisionAsync(dbContext, id);
        if (loaded is null)
        {
            return TypedResults.NotFound();
        }

        var (activity, application) = loaded.Value;
        var now = clock.GetUtcNow();
        var error = ApplicationRules.CheckCanDecide(new DecisionCheck(activity, application, user.UserId(), now, Decision.Accept));
        if (error is not null)
        {
            return ProblemFor(error);
        }

        application.Accept(now);
        activity.MarkFilled();
        await ExpireOtherPendingApplicationsAsync(dbContext, application, now);
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        return TypedResults.Ok(new AcceptedApplicationResponse(application.Id, ActivityRules.IsShortNotice(activity.StartsAt, now)));
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Refuse(
        Guid id,
        ClaimsPrincipal user,
        OnygoDbContext dbContext,
        TimeProvider clock)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var loaded = await LoadForDecisionAsync(dbContext, id);
        if (loaded is null)
        {
            return TypedResults.NotFound();
        }

        var (activity, application) = loaded.Value;
        var now = clock.GetUtcNow();
        var error = ApplicationRules.CheckCanDecide(new DecisionCheck(activity, application, user.UserId(), now, Decision.Refuse));
        if (error is not null)
        {
            return ProblemFor(error);
        }

        // R2 : la place se libère d'elle-même, une candidature refusée ne compte plus parmi celles en attente.
        application.Refuse(now);
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        return TypedResults.NoContent();
    }

    private static async Task<ApplyCheck> ReadApplyCheckAsync(
        OnygoDbContext dbContext,
        Activity activity,
        string applicantId,
        TimeProvider clock)
    {
        var (dayStart, dayEnd) = ParisCalendar.DayInUtc(ParisCalendar.Today(clock));
        var hasAlreadyApplied = await dbContext.Applications.AnyAsync(application =>
            application.ActivityId == activity.Id && application.ApplicantId == applicantId);
        var pendingCount = await dbContext.Applications.CountAsync(application =>
            application.ActivityId == activity.Id && application.Status == ApplicationStatus.Pending);
        var todayCount = await dbContext.Applications.CountAsync(application =>
            application.ApplicantId == applicantId && application.AppliedAt >= dayStart && application.AppliedAt < dayEnd);

        return new ApplyCheck(activity, applicantId, clock.GetUtcNow(), hasAlreadyApplied, pendingCount, todayCount);
    }

    /// <summary>
    /// Verrouille l'activité puis relit la candidature : relue avant le verrou, elle pourrait déjà
    /// avoir été acceptée, refusée ou expirée par une requête concurrente.
    /// </summary>
    private static async Task<(Activity Activity, ActivityApplication Application)?> LoadForDecisionAsync(
        OnygoDbContext dbContext,
        Guid applicationId)
    {
        var activityId = await dbContext.Applications
            .Where(application => application.Id == applicationId)
            .Select(application => (Guid?)application.ActivityId)
            .SingleOrDefaultAsync();
        if (activityId is null)
        {
            return null;
        }

        var activity = await LockActivityAsync(dbContext, activityId.Value);
        var application = await dbContext.Applications.SingleAsync(application => application.Id == applicationId);
        return (activity!, application);
    }

    private static async Task ExpireOtherPendingApplicationsAsync(
        OnygoDbContext dbContext,
        ActivityApplication accepted,
        DateTimeOffset now)
    {
        var others = await dbContext.Applications
            .Where(application => application.ActivityId == accepted.ActivityId
                && application.Status == ApplicationStatus.Pending
                && application.Id != accepted.Id)
            .ToListAsync();
        others.ForEach(application => application.Expire(now));
    }

    // R1 et R4 : sérialise les candidatures et décisions sur une même activité.
    private static Task<Activity?> LockActivityAsync(OnygoDbContext dbContext, Guid activityId) =>
        dbContext.Activities
            .FromSql($"""SELECT * FROM "Activities" WHERE "Id" = {activityId} FOR UPDATE""")
            .SingleOrDefaultAsync();

    // R6 : sérialise les candidatures d'un même utilisateur, pour le comptage journalier.
    private static Task LockUserAsync(OnygoDbContext dbContext, string userId) =>
        dbContext.Database.ExecuteSqlAsync($"""SELECT 1 FROM "AspNetUsers" WHERE "Id" = {userId} FOR UPDATE""");

    private static ProblemHttpResult ProblemFor(string errorCode) => errorCode switch
    {
        ApplicationErrorCodes.CannotApplyToOwnActivity or ApplicationErrorCodes.NotOrganizer =>
            BusinessProblem.Forbidden(errorCode),
        _ => BusinessProblem.Conflict(errorCode),
    };

    private static MyApplicationResponse ToMyApplication(ActivityApplication application, Activity activity, DateTimeOffset now) =>
        new(
            application.Id,
            activity.Id,
            activity.Title,
            activity.StartsAt,
            ToMyStatus(application.Status),
            application.Message,
            application.AppliedAt,
            ActivityRules.IsShortNotice(activity.StartsAt, now));

    private static MyApplicationStatus ToMyStatus(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Pending => MyApplicationStatus.Pending,
        ApplicationStatus.Accepted => MyApplicationStatus.Accepted,
        _ => throw new InvalidOperationException($"A {status} application must never be shown to its applicant (R9)."),
    };
}

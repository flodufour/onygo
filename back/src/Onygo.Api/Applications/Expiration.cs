using Microsoft.EntityFrameworkCore;
using Onygo.Api.Activities;
using Onygo.Api.Infrastructure.Persistence;

namespace Onygo.Api.Applications;

public sealed record ExpirationResult(int ExpiredActivities, int ExpiredApplications);

/// <summary>
/// R5 : 2 h avant le début, les candidatures encore en attente expirent, et une activité encore ouverte
/// (donc sans personne acceptée) expire aussi. Idempotent : peut tourner sur plusieurs instances à la fois.
/// </summary>
public sealed class ExpirationService(OnygoDbContext dbContext, TimeProvider clock)
{
    public async Task<ExpirationResult> ExpireOverdueAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var closingLimit = now + ActivityRules.ApplicationsCloseBeforeStart;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Les activités d'abord : leur UPDATE attend le verrou d'une candidature ou d'une acceptation en cours
        // sur la même activité, et passe après elle. Les candidatures en attente sont ensuite relues à jour.
        var expiredActivities = await dbContext.Activities
            .Where(activity => activity.Status == ActivityStatus.Open && activity.StartsAt <= closingLimit)
            .ExecuteUpdateAsync(setters => setters.SetProperty(activity => activity.Status, ActivityStatus.Expired), cancellationToken);

        var expiredApplications = await dbContext.Applications
            .Where(application => application.Status == ApplicationStatus.Pending
                && dbContext.Activities.Any(activity => activity.Id == application.ActivityId && activity.StartsAt <= closingLimit))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(application => application.Status, ApplicationStatus.Expired)
                    .SetProperty(application => application.DecidedAt, now),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new ExpirationResult(expiredActivities, expiredApplications);
    }
}

/// <summary>
/// Lance l'expiration à intervalle régulier pendant toute la vie de l'API.
/// </summary>
public sealed class ExpirationWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<ExpirationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ExpirationService>().ExpireOverdueAsync(stoppingToken);
            if (result.ExpiredActivities > 0 || result.ExpiredApplications > 0)
            {
                logger.LogInformation(
                    "Expired {ExpiredActivities} activities and {ExpiredApplications} applications",
                    result.ExpiredActivities,
                    result.ExpiredApplications);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Une base momentanément indisponible ne doit pas arrêter l'API : on journalise et on réessaie au prochain passage.
            logger.LogError(exception, "Expiration run failed, retrying at next tick");
        }
    }
}

namespace Onygo.Api.Applications;

/// <summary>
/// Sérialisé en texte en base. Jamais exposé tel quel au candidat (R9) : voir MyApplicationStatus.
/// </summary>
public enum ApplicationStatus
{
    Pending,
    Accepted,
    Refused,
    Expired,
}

/// <summary>
/// Candidature d'un utilisateur à une activité. Jamais supprimée (R10) : seul son statut évolue.
/// </summary>
public class ActivityApplication
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required Guid ActivityId { get; init; }

    public required string ApplicantId { get; init; }

    public string? Message { get; init; }

    public ApplicationStatus Status { get; private set; } = ApplicationStatus.Pending;

    public required DateTimeOffset AppliedAt { get; init; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public void Accept(DateTimeOffset now) => LeavePending(ApplicationStatus.Accepted, now);

    public void Refuse(DateTimeOffset now) => LeavePending(ApplicationStatus.Refused, now);

    public void Expire(DateTimeOffset now) => LeavePending(ApplicationStatus.Expired, now);

    private void LeavePending(ApplicationStatus newStatus, DateTimeOffset now)
    {
        if (Status != ApplicationStatus.Pending)
        {
            throw new InvalidOperationException($"Only a pending application can become {newStatus} (current status: {Status}).");
        }

        Status = newStatus;
        DecidedAt = now;
    }
}

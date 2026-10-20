using Onygo.Api.Applications;

namespace Onygo.Api.UnitTests.Applications;

public class ActivityApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 8, 0, 0, TimeSpan.Zero);

    private static ActivityApplication APendingApplication() => new()
    {
        ActivityId = Guid.NewGuid(),
        ApplicantId = "applicant",
        AppliedAt = Now.AddHours(-1),
    };

    [Fact]
    public void Accept_PendingApplication_RecordsDecisionTime()
    {
        var application = APendingApplication();

        application.Accept(Now);

        Assert.Equal(ApplicationStatus.Accepted, application.Status);
        Assert.Equal(Now, application.DecidedAt);
    }

    [Fact]
    public void Accept_AlreadyRefusedApplication_Throws()
    {
        var application = APendingApplication();
        application.Refuse(Now);

        Assert.Throws<InvalidOperationException>(() => application.Accept(Now));
    }
}

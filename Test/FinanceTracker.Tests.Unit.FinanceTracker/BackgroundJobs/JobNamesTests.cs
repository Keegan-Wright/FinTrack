using FinanceTracker.BackgroundJobs;
using FinanceTracker.Infrastructure.Automation;

namespace FinanceTracker.Tests.Unit.FinanceTracker.BackgroundJobs;

/// <summary>
/// Infrastructure names the jobs it starts and reads, the job classes have to be named the same.
/// </summary>
public class JobNamesTests
{
    [Test]
    public async Task JobNames_AreTheJobClassNames()
    {
        using (Assert.Multiple())
        {
            await Assert.That(JobNames.SyncAll).IsEqualTo(nameof(SyncAllOpenBankingDetailsAsync));
            await Assert.That(JobNames.SyncUser).IsEqualTo(nameof(SyncUserOpenBankingDetailsAsync));
            await Assert.That(JobNames.MonitorTokens).IsEqualTo(nameof(MonitorOpenBankingTokenExpirationAsync));
            await Assert.That(JobNames.MoveGoalsOn).IsEqualTo(nameof(MoveEventSavingsGoalsOnAsync));
        }
    }

    [Test]
    public async Task ScheduledJobs_StartOnSchedulesQuartzCanRead()
    {
        using (Assert.Multiple())
        {
            foreach (ScheduledJob job in ScheduledJobs.All)
            {
                await Assert.That(JobSchedules.IsReadable(job.Expression)).IsTrue().Because(job.Function);
            }
        }
    }
}

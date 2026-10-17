using FinanceTracker.Infrastructure.Dashboard;
using FinanceTracker.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace FinanceTracker.Infrastructure.Tests.Services.Dashboard;

public class DashboardServiceTests : ServiceTestsFixtureBase
{
    [Test]
    public async Task GetSpentInTimePeriod_ExcludesTransfers_AndSplitsInOutTotals()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"dashboard-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        DateTime now = DateTime.UtcNow;
        await SeedAccountAsync(factory, PrimaryUserId, "Daily", _cancellationTokenSource.Token,
            Transaction("d1", "Incoming", 100, now, category: "SALARY"),
            Transaction("d2", "Outgoing", -25, now, category: "SHOPPING"),
            Transaction("d3", "Transfer", -500, now, category: "TRANSFER"));

        var subject = new DashboardService(BuildUser(PrimaryUserId), factory, NullLogger<DashboardService>.Instance);
        var result = await subject.GetSpentInTimePeriod(now.AddDays(-1), now.AddDays(1), _cancellationTokenSource.Token);

        using (Assert.Multiple())
        {
            await Assert.That(result.TotalIn).IsEqualTo(100);
            await Assert.That(result.TotalOut).IsEqualTo(-25);
        }
    }

    [Test]
    public async Task GetSpentInTimePeriod_WhenUserHasNoTransactions_ReturnsZeroTotals()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"dashboard-empty-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        var subject = new DashboardService(BuildUser(PrimaryUserId), factory, NullLogger<DashboardService>.Instance);
        DateTime now = DateTime.UtcNow;

        var result = await subject.GetSpentInTimePeriod(now.AddDays(-1), now.AddDays(1), _cancellationTokenSource.Token);

        using (Assert.Multiple())
        {
            await Assert.That(result.TotalIn).IsEqualTo(0);
            await Assert.That(result.TotalOut).IsEqualTo(0);
        }
    }
}

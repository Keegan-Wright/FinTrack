using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Reports;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Infrastructure.Reports;
using FinanceTracker.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Reports;

public class ReportServiceTests : ServiceTestsFixtureBase
{
    [Test]
    public async Task GetSpentInTimePeriodReportAsync_ReturnsAggregatedTotals()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"report-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        DateTime now = DateTime.UtcNow;
        await SeedAccountAsync(factory, PrimaryUserId, "Main", _cancellationTokenSource.Token,
            Transaction("rp-1", "Income", 200, now, category: "SALARY"),
            Transaction("rp-2", "Expense", -50, now, category: "FOOD"));

        var openBanking = Substitute.For<IOpenBankingService>();
        var subject = new ReportService(BuildUser(PrimaryUserId), factory, openBanking, NullLogger<ReportService>.Instance);

        var request = new BaseReport
        {
            SyncTypes = SyncTypes.All,
            AccountIds = null,
            ProviderIds = null,
            Categories = null,
            Types = null,
            Tags = null
        };

        var results = new List<decimal>();
        await foreach (var row in subject.GetSpentInTimePeriodReportAsync(request, _cancellationTokenSource.Token))
        {
            results.Add(row.TotalIn + row.TotalOut);
        }

        await Assert.That(results.Single()).IsEqualTo(150);
    }

    [Test]
    public async Task GetSpentInTimePeriodReportAsync_WhenUserHasNoTransactions_ReturnsEmpty()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"report-empty-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        var openBanking = Substitute.For<IOpenBankingService>();
        var subject = new ReportService(BuildUser(PrimaryUserId), factory, openBanking, NullLogger<ReportService>.Instance);

        var request = new BaseReport
        {
            SyncTypes = SyncTypes.All,
            AccountIds = null,
            ProviderIds = null,
            Categories = null,
            Types = null,
            Tags = null
        };

        var results = new List<decimal>();
        await foreach (var row in subject.GetSpentInTimePeriodReportAsync(request, _cancellationTokenSource.Token))
        {
            results.Add(row.TotalIn + row.TotalOut);
        }

        await Assert.That(results.Count).IsEqualTo(0);
    }
}

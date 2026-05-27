using System.Collections.Immutable;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using FinanceTracker.Enums;
using FinanceTracker.Models.Request.Reports;
using FinanceTracker.Services.OpenBanking;
using FinanceTracker.Services.Reports;
using FinanceTracker.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Tests.Services.Reports;

public class ReportServiceTests : ServiceTestsFixtureBase
{
    [Test]
    public async Task GetSpentInTimePeriodReportAsync_ReturnsAggregatedTotals()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"report-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        DateTime now = DateTime.UtcNow;
        await using (FinanceTrackerContext context = await factory.CreateDbContextAsync(_cancellationTokenSource.Token))
        {
            FinanceTrackerUser user = await context.Users.Include(x => x.Providers)
                .SingleAsync(x => x.Id == PrimaryUserId, _cancellationTokenSource.Token);

            var provider = new OpenBankingProvider
            {
                Name = "Report Provider",
                AccessCode = "code",
                OpenBankingProviderId = "provider",
                Logo = [4],
                Accounts = [],
                Scopes = [],
                Transactions = [],
                Syncronisations = []
            };

            var account = new OpenBankingAccount
            {
                OpenBankingAccountId = "acc",
                AccountType = "CURRENT",
                DisplayName = "Main",
                Currency = "GBP",
                ProviderId = provider.Id,
                Transactions = [],
                StandingOrders = [],
                DirectDebits = [],
                Syncronisations = []
            };

            account.Transactions!.Add(new OpenBankingTransaction
            {
                Description = "Income",
                TransactionType = "CREDIT",
                TransactionCategory = "SALARY",
                Amount = 200,
                Currency = "GBP",
                TransactionId = "rp-1",
                TransactionTime = now,
                Pending = false,
                ProviderId = provider.Id,
                AccountId = account.Id,
                Classifications = []
            });

            account.Transactions.Add(new OpenBankingTransaction
            {
                Description = "Expense",
                TransactionType = "DEBIT",
                TransactionCategory = "FOOD",
                Amount = -50,
                Currency = "GBP",
                TransactionId = "rp-2",
                TransactionTime = now,
                Pending = false,
                ProviderId = provider.Id,
                AccountId = account.Id,
                Classifications = []
            });

            foreach (OpenBankingTransaction tx in account.Transactions)
            {
                provider.Transactions!.Add(tx);
            }

            provider.Accounts!.Add(account);
            user.Providers!.Add(provider);
            await context.SaveChangesAsync(_cancellationTokenSource.Token);
        }

        var openBanking = Substitute.For<IOpenBankingService>();
        var subject = new ReportService(BuildUser(PrimaryUserId), factory, openBanking, NullLogger<ReportService>.Instance);

        var request = new BaseReportRequest
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

        var request = new BaseReportRequest
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

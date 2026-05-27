using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using FinanceTracker.Services.Dashboard;
using FinanceTracker.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Services.Dashboard;

public class DashboardServiceTests : ServiceTestsFixtureBase
{
    [Test]
    public async Task GetSpentInTimePeriod_ExcludesTransfers_AndSplitsInOutTotals()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"dashboard-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        DateTime now = DateTime.UtcNow;
        await using (FinanceTrackerContext context = await factory.CreateDbContextAsync(_cancellationTokenSource.Token))
        {
            FinanceTrackerUser user = await context.Users.Include(x => x.Providers)
                .SingleAsync(x => x.Id == PrimaryUserId, _cancellationTokenSource.Token);

            OpenBankingProvider provider = new()
            {
                Name = "Dash Provider",
                AccessCode = "code",
                OpenBankingProviderId = "provider",
                Logo = [7],
                Accounts = [],
                Scopes = [],
                Transactions = [],
                Syncronisations = []
            };

            OpenBankingAccount account = new()
            {
                OpenBankingAccountId = "acc",
                AccountType = "CURRENT",
                DisplayName = "Daily",
                Currency = "GBP",
                ProviderId = provider.Id,
                Transactions = [],
                StandingOrders = [],
                DirectDebits = [],
                Syncronisations = []
            };

            account.Transactions!.Add(new OpenBankingTransaction
            {
                Description = "Incoming",
                TransactionType = "CREDIT",
                TransactionCategory = "SALARY",
                Amount = 100,
                Currency = "GBP",
                TransactionId = "d1",
                TransactionTime = now,
                Pending = false,
                ProviderId = provider.Id,
                AccountId = account.Id,
                Classifications = []
            });

            account.Transactions.Add(new OpenBankingTransaction
            {
                Description = "Outgoing",
                TransactionType = "DEBIT",
                TransactionCategory = "SHOPPING",
                Amount = -25,
                Currency = "GBP",
                TransactionId = "d2",
                TransactionTime = now,
                Pending = false,
                ProviderId = provider.Id,
                AccountId = account.Id,
                Classifications = []
            });

            account.Transactions.Add(new OpenBankingTransaction
            {
                Description = "Transfer",
                TransactionType = "DEBIT",
                TransactionCategory = "TRANSFER",
                Amount = -500,
                Currency = "GBP",
                TransactionId = "d3",
                TransactionTime = now,
                Pending = false,
                ProviderId = provider.Id,
                AccountId = account.Id,
                Classifications = []
            });

            provider.Accounts!.Add(account);
            user.Providers!.Add(provider);
            await context.SaveChangesAsync(_cancellationTokenSource.Token);
        }

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

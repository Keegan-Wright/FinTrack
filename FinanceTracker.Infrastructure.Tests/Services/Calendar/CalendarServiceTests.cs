using FinanceTracker.Domain;
using FinanceTracker.Infrastructure.Calendar;
using FinanceTracker.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Infrastructure.Tests.Services.Calendar;

public class CalendarServiceTests : ServiceTestsFixtureBase
{
    [Test]
    public async Task GetMonthItemsAsync_ReturnsDayEntriesAndIncludesTransactionsOnMatchingDate()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"calendar-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        DateTime txDate = new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc);
        await using (FinanceTrackerContext context = await factory.CreateDbContextAsync(_cancellationTokenSource.Token))
        {
            FinanceTrackerUser user = await context.Users.Include(x => x.Providers)
                .SingleAsync(x => x.Id == PrimaryUserId, _cancellationTokenSource.Token);

            var provider = new OpenBankingProvider
            {
                Name = "Calendar Bank",
                AccessCode = "code",
                OpenBankingProviderId = "provider",
                Logo = [9],
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
                Description = "CalendarTx",
                TransactionType = "DEBIT",
                TransactionCategory = "SHOPPING",
                Amount = -12,
                Currency = "GBP",
                TransactionId = "cal-1",
                TransactionTime = txDate,
                Pending = false,
                ProviderId = provider.Id,
                AccountId = account.Id,
                Classifications = []
            });

            provider.Accounts!.Add(account);
            user.Providers!.Add(provider);
            await context.SaveChangesAsync(_cancellationTokenSource.Token);
        }

        var subject = new CalendarService(BuildUser(PrimaryUserId), factory, NullLogger<CalendarService>.Instance);

        List<(DateTime Date, int Transactions)> days = [];
        await foreach (var item in subject.GetMonthItemsAsync(5, 2026, _cancellationTokenSource.Token))
        {
            days.Add((item.Date, item.Transactions.Count()));
        }

        using (Assert.Multiple())
        {
            await Assert.That(days.Count).IsEqualTo(31);
            await Assert.That(days.Single(x => x.Date.Date == txDate.Date).Transactions).IsEqualTo(1);
        }
    }

    [Test]
    public async Task GetMonthItemsAsync_WhenUserHasNoTransactions_ReturnsZeroTransactionsForAllDays()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"calendar-empty-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        var subject = new CalendarService(BuildUser(PrimaryUserId), factory, NullLogger<CalendarService>.Instance);

        var days = new List<int>();
        await foreach (var item in subject.GetMonthItemsAsync(5, 2026, _cancellationTokenSource.Token))
        {
            days.Add(item.Transactions.Count());
        }

        using (Assert.Multiple())
        {
            await Assert.That(days.Count).IsEqualTo(31);
            await Assert.That(days.All(static x => x == 0)).IsTrue();
        }
    }
}

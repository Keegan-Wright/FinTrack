using System.Collections.Immutable;
using FinanceTracker.Data;
using FinanceTracker.Data.Models;
using FinanceTracker.Enums;
using FinanceTracker.Models.Request.Transaction;
using FinanceTracker.Services.OpenBanking;
using FinanceTracker.Services.Transactions;
using FinanceTracker.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Tests.Services.Transactions;

public class TransactionsServiceTests : ServiceTestsFixtureBase
{
    [Test]
    public async Task GetAllTransactionsAsync_AppliesSearchAndDateFilters()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"transactions-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        DateTime now = DateTime.UtcNow;
        await using (FinanceTrackerContext context = await factory.CreateDbContextAsync(_cancellationTokenSource.Token))
        {
            FinanceTrackerUser user = await context.Users.Include(x => x.Providers)
                .SingleAsync(x => x.Id == PrimaryUserId, _cancellationTokenSource.Token);

            var provider = new OpenBankingProvider
            {
                Name = "Tx Provider",
                AccessCode = "code",
                OpenBankingProviderId = "provider",
                Logo = [1],
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
                Description = "Coffee Shop",
                TransactionType = "DEBIT",
                TransactionCategory = "FOOD",
                Amount = -4,
                Currency = "GBP",
                TransactionId = "tx-1",
                TransactionTime = now,
                Pending = false,
                ProviderId = provider.Id,
                AccountId = account.Id,
                Classifications = []
            });

            account.Transactions.Add(new OpenBankingTransaction
            {
                Description = "Salary",
                TransactionType = "CREDIT",
                TransactionCategory = "INCOME",
                Amount = 1000,
                Currency = "GBP",
                TransactionId = "tx-2",
                TransactionTime = now.AddDays(-20),
                Pending = false,
                ProviderId = provider.Id,
                AccountId = account.Id,
                Classifications = []
            });

            provider.Accounts!.Add(account);
            user.Providers!.Add(provider);
            await context.SaveChangesAsync(_cancellationTokenSource.Token);
        }

        var openBanking = Substitute.For<IOpenBankingService>();
        var subject = new TransactionsService(BuildUser(PrimaryUserId), factory, openBanking,
            NullLogger<TransactionsService>.Instance);

        var filter = new FilteredTransactionsRequest
        {
            SearchTerm = "coffee",
            FromDate = now.AddDays(-1),
            ToDate = now.AddDays(1),
            AccountIds = ImmutableList<Guid>.Empty,
            ProviderIds = ImmutableList<Guid>.Empty,
            Categories = ImmutableList<string>.Empty,
            Types = ImmutableList<string>.Empty,
            Tags = ImmutableList<string>.Empty
        };

        List<string> descriptions = [];
        await foreach (var tx in subject.GetAllTransactionsAsync(filter, SyncTypes.All, _cancellationTokenSource.Token))
        {
            descriptions.Add(tx.Description);
        }

        await Assert.That(descriptions).IsEquivalentTo(["Coffee Shop"]);
    }

    [Test]
    public async Task GetAllTransactionsAsync_WhenUserHasZeroTransactions_ReturnsEmpty()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"transactions-empty-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        var openBanking = Substitute.For<IOpenBankingService>();
        var subject = new TransactionsService(BuildUser(PrimaryUserId), factory, openBanking,
            NullLogger<TransactionsService>.Instance);

        var filter = new FilteredTransactionsRequest
        {
            SearchTerm = string.Empty,
            FromDate = DateTime.UtcNow.AddDays(-7),
            ToDate = DateTime.UtcNow.AddDays(1),
            AccountIds = ImmutableList<Guid>.Empty,
            ProviderIds = ImmutableList<Guid>.Empty,
            Categories = ImmutableList<string>.Empty,
            Types = ImmutableList<string>.Empty,
            Tags = ImmutableList<string>.Empty
        };

        var descriptions = new List<string>();
        await foreach (var tx in subject.GetAllTransactionsAsync(filter, SyncTypes.All, _cancellationTokenSource.Token))
        {
            descriptions.Add(tx.Description);
        }

        await Assert.That(descriptions.Count).IsEqualTo(0);
    }
}

using System.Collections.Immutable;
using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Infrastructure.Transactions;
using FinanceTracker.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Transactions;

public class TransactionsServiceTests : ServiceTestsFixtureBase
{
    [Test]
    public async Task GetAllTransactionsAsync_AppliesSearchAndDateFilters()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"transactions-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        DateTime now = DateTime.UtcNow;
        await SeedAccountAsync(factory, PrimaryUserId, "Main", _cancellationTokenSource.Token,
            Transaction("tx-1", "Coffee Shop", -4, now, category: "FOOD"),
            Transaction("tx-2", "Salary", 1000, now.AddDays(-20), category: "INCOME"));

        var openBanking = Substitute.For<IOpenBankingService>();
        var subject = new TransactionsService(BuildUser(PrimaryUserId), factory, openBanking,
            NullLogger<TransactionsService>.Instance);

        var filter = new FilterTransactions
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

        var filter = new FilterTransactions
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

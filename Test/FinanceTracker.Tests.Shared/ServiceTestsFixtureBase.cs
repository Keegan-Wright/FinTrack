using System.Security.Claims;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Encryption;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Shared;

public abstract class ServiceTestsFixtureBase : TestFixtureBase
{
    protected static readonly Guid PrimaryUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid SecondaryUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected static ClaimsPrincipal BuildUser(Guid userId)
    {
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], "test");

        return new ClaimsPrincipal(identity);
    }

    protected static SymmetricEncryptionService BuildEncryptionService() => new(new EncryptionConfiguration
    {
        SymmetricKey = "SymmetricKeyForTests",
        Iterations = 1000,
        SymmetricSalt = "SymmetricSaltForTests"
    });

    protected static IDbContextFactory<FinanceTrackerContext> BuildFactory(string databaseName)
    {
        var options = new DbContextOptionsBuilder<FinanceTrackerContext>()
           // UseInMemoryDatabase(databaseName)
            .Options;

        return new InMemoryFinanceTrackerContextFactory(options, BuildEncryptionService());
    }

    protected static async Task SeedUsersAsync(IDbContextFactory<FinanceTrackerContext> contextFactory, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        context.Users.AddRange(
            new FinanceTrackerUser("Primary", "User", "primary@test.local", "primary@test.local")
            {
                Id = PrimaryUserId
            },
            new FinanceTrackerUser("Secondary", "User", "secondary@test.local", "secondary@test.local")
            {
                Id = SecondaryUserId
            }
        );

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Seeds a connection with a single account for <paramref name="userId"/> and imports the given transactions.
    /// </summary>
    protected static async Task<BankAccount> SeedAccountAsync(IDbContextFactory<FinanceTrackerContext> contextFactory,
        Guid userId, string displayName, CancellationToken cancellationToken, params TransactionDetails[] transactions)
    {
        await using FinanceTrackerContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        BankConnection connection = new(userId, $"provider-{Guid.NewGuid()}", "Test Bank", "access-code",
            ["accounts", "transactions"], [1, 2, 3]);
        BankAccount account = new(userId, connection.Id, $"acc-{Guid.NewGuid()}", "CURRENT", displayName, "GBP");

        context.BankConnections.Add(connection);
        context.BankAccounts.Add(account);
        context.BankTransactions.AddRange(transactions.Select(x => BankTransaction.Import(account, x)));

        await context.SaveChangesAsync(cancellationToken);
        return account;
    }

    protected static TransactionDetails Transaction(string externalId, string description, decimal amount,
        DateTime occurredAt, string category = "SHOPPING", bool isPending = false, params string[] classifications) =>
        new(externalId, description, decimal.IsNegative(amount) ? TransactionType.Debit : TransactionType.Credit,
            category, amount, "GBP", occurredAt, isPending, classifications);

    private sealed class InMemoryFinanceTrackerContextFactory(
        DbContextOptions<FinanceTrackerContext> options,
        ISymmetricEncryptionService encryptionService) : IDbContextFactory<FinanceTrackerContext>
    {
        public FinanceTrackerContext CreateDbContext() => new(options, encryptionService);

        public ValueTask<FinanceTrackerContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(CreateDbContext());
    }
}

using System.Security.Claims;
using FinanceTracker.Domain;
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
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new InMemoryFinanceTrackerContextFactory(options, BuildEncryptionService());
    }

    protected static async Task SeedUsersAsync(IDbContextFactory<FinanceTrackerContext> contextFactory, CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        context.Users.AddRange(
            new FinanceTrackerUser
            {
                Id = PrimaryUserId,
                Email = "primary@test.local",
                UserName = "primary@test.local",
                FirstName = "Primary",
                LastName = "User",
                BudgetCategories = [],
                HouseholdMembers = [],
                CustomClassifications = [],
                Debts = [],
                OpenBankingAccessTokens = [],
                Providers = []
            },
            new FinanceTrackerUser
            {
                Id = SecondaryUserId,
                Email = "secondary@test.local",
                UserName = "secondary@test.local",
                FirstName = "Secondary",
                LastName = "User",
                BudgetCategories = [],
                HouseholdMembers = [],
                CustomClassifications = [],
                Debts = [],
                OpenBankingAccessTokens = [],
                Providers = []
            }
        );

        await context.SaveChangesAsync(cancellationToken);
    }

    private sealed class InMemoryFinanceTrackerContextFactory(
        DbContextOptions<FinanceTrackerContext> options,
        ISymmetricEncryptionService encryptionService) : IDbContextFactory<FinanceTrackerContext>
    {
        public FinanceTrackerContext CreateDbContext() => new(options, encryptionService);

        public ValueTask<FinanceTrackerContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(CreateDbContext());
    }
}

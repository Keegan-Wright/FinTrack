using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Unit.Infrastructure.Persistence;

public class BankAccountPersistenceTests : InfrastructureTestBase
{
    private const string Username = "primary";

    [Test]
    public async Task Accounts_WithoutAnIdentifierOrCard_LoadWithAnUnknownIdentifierAndNoCard()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("current", "bank"))
            .Do(s => s.BankAccountScenario.WithAccount("savings", "bank"));

        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        List<BankAccount> loaded = await context.BankAccounts.ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(loaded).Count().IsEqualTo(2);
            await Assert.That(loaded.All(static x => !x.Identifier.IsKnown)).IsTrue();
            await Assert.That(loaded.All(static x => x.Card == CardDetails.None())).IsTrue();
        }
    }

    [Test]
    public async Task Card_WithItsDetails_LoadsThem()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username));
        CardDetails details = new("VISA", "CREDIT", "1234", null, null);

        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            BankAccount card = new(Scenario.UserMappings[Username], Scenario.ConnectionMappings["bank"], "card-1",
                "CREDIT_CARD", "Card", "GBP", BankAccountKind.Card);
            card.UpdateCard(details);
            context.BankAccounts.Add(card);
            await context.SaveChangesAsync(CancellationToken);
        }

        // Act
        await using FinanceTrackerContext reading = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        BankAccount loaded = await reading.BankAccounts.SingleAsync(CancellationToken);

        // Assert
        await Assert.That(loaded.Card).IsEqualTo(details);
    }
}

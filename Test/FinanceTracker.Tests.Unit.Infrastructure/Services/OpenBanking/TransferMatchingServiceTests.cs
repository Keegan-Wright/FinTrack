using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.OpenBanking;

public class TransferMatchingServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    private static readonly DateTime Monday = new(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task MatchAsync_SavesTheDecisionOnBothHalves()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.TransactionScenario.WithTransaction("out", "mine-current",
                Details(-50m, reference: "222222 22222222")))
            .Do(s => s.TransactionScenario.WithTransaction("in", "mine-savings", Details(50m)));

        TransferMatchingService subject = BuildSubject(Username);

        // Act
        TransferMatchSummary summary = await subject.MatchAsync(CancellationToken);

        // Assert
        Dictionary<Guid, BankTransaction> stored = (await LoadTransactionsAsync()).ToDictionary(x => x.Id);
        BankTransaction outgoing = stored[Scenario.TransactionMappings["out"]];
        BankTransaction incoming = stored[Scenario.TransactionMappings["in"]];

        using (Assert.Multiple())
        {
            await Assert.That(summary).IsEqualTo(new TransferMatchSummary(2, 0, 0));
            await Assert.That(outgoing.Transfer!.Decision).IsEqualTo(TransferDecision.Transfer);
            await Assert.That(outgoing.Transfer.Source).IsEqualTo(DecisionSource.Matcher);
            await Assert.That(outgoing.Transfer.Reason).IsEqualTo(TransferReason.OwnAccountReference);
            await Assert.That(outgoing.Transfer.PartnerTransactionId).IsEqualTo(incoming.Id);
            await Assert.That(outgoing.Transfer.PartnerAccountId)
                .IsEqualTo(Scenario.AccountMappings["mine-savings"]);
            await Assert.That(incoming.Transfer!.PartnerTransactionId).IsEqualTo(outgoing.Id);
        }
    }

    [Test]
    public async Task MatchAsync_LeavesOtherUsersTransactionsAlone()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine");
        await SeedAccountsAsync(OtherUsername, "theirs")
            .Do(s => s.TransactionScenario.WithTransaction("out", "theirs-current",
                Details(-50m, labelled: true)))
            .Do(s => s.TransactionScenario.WithTransaction("in", "theirs-savings", Details(50m)));

        TransferMatchingService subject = BuildSubject(Username);

        // Act
        TransferMatchSummary summary = await subject.MatchAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(summary).IsEqualTo(new TransferMatchSummary(0, 0, 0));
            await Assert.That((await LoadTransactionsAsync()).Select(x => x.Transfer)).All().Satisfy(x => x.IsNull());
        }
    }

    [Test]
    public async Task MatchAsync_KeepsTheUsersDecisionAcrossRuns()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.TransactionScenario.WithTransaction("out", "mine-current", Details(-50m, labelled: true)))
            .Do(s => s.TransactionScenario.WithTransaction("in", "mine-savings", Details(50m)));

        TransferMatchingService subject = BuildSubject(Username);
        await subject.MatchAsync(CancellationToken);
        await MarkNotATransferAsync(Scenario.TransactionMappings["out"]);

        // Act
        await subject.MatchAsync(CancellationToken);

        // Assert
        Dictionary<Guid, BankTransaction> stored = (await LoadTransactionsAsync()).ToDictionary(x => x.Id);

        using (Assert.Multiple())
        {
            await Assert.That(stored[Scenario.TransactionMappings["out"]].Transfer!.Decision)
                .IsEqualTo(TransferDecision.NotATransfer);
            await Assert.That(stored[Scenario.TransactionMappings["in"]].Transfer).IsNull();
        }
    }

    /// <summary>
    /// A current account and a savings account named <c>{prefix}-current</c> and <c>{prefix}-savings</c>, the
    /// savings one with sort code 222222 and number 22222222.
    /// </summary>
    private Task<TestingScenario> SeedAccountsAsync(string username, string prefix) =>
        Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BankConnectionScenario.WithConnection(prefix, username))
            .Do(s => s.BankAccountScenario.WithAccount($"{prefix}-current", prefix))
            .Do(s => s.BankAccountScenario.WithAccount($"{prefix}-savings", prefix, (userId, connectionId) =>
            {
                BankAccount account = new BankAccountFaker(userId, connectionId).Generate();
                account.UpdateIdentifier(AccountIdentifier.Create("222222", "22222222", null));
                return account;
            }));

    private static TransactionDetails Details(decimal amount, bool labelled = false, string? reference = null) =>
        new TransactionDetailsFaker().Generate() with
        {
            Amount = amount,
            Type = amount < 0 ? TransactionType.Debit : TransactionType.Credit,
            Category = "PURCHASE",
            LabelledAsTransfer = labelled,
            Currency = "GBP",
            OccurredAt = Monday,
            IsPending = false,
            Metadata = new TransactionMetadata(reference, null, null, null, null)
        };

    private async Task MarkNotATransferAsync(Guid transactionId)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        BankTransaction transaction =
            await context.BankTransactions.SingleAsync(x => x.Id == transactionId, CancellationToken);
        transaction.MarkNotATransfer(Monday);
        await context.SaveChangesAsync(CancellationToken);
    }

    private async Task<List<BankTransaction>> LoadTransactionsAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.BankTransactions.ToListAsync(CancellationToken);
    }

    private TransferMatchingService BuildSubject(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<TransferMatchingService>.Instance,
            TimeProvider.System);
}

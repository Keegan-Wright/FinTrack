using FinanceTracker.Contracts.Transactions;
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
using Transaction = FinanceTracker.Contracts.Transactions.Transaction;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.OpenBanking;

public class TransfersServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    private static readonly DateTime Yesterday = DateTime.UtcNow.AddDays(-1);

    [Test]
    public async Task GetTransfersToReviewAsync_ReturnsTheUsersWithTheirSuggestedPartner()
    {
        // Arrange, an unlabelled mirror pair is suggested for review
        await SeedUnlabelledPairAsync(Username, "mine");
        await SeedUnlabelledPairAsync(OtherUsername, "theirs");
        await MatchAsync(Username);
        await MatchAsync(OtherUsername);

        TransfersService subject = BuildSubject(Username);

        // Act
        List<Transaction> toReview = await subject.GetTransfersToReviewAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        Transaction outgoing = toReview.Single(x => x.TransactionId == Scenario.TransactionMappings["mine-out"]);

        using (Assert.Multiple())
        {
            await Assert.That(toReview).Count().IsEqualTo(2);
            await Assert.That(outgoing.Transfer!.State).IsEqualTo(TransferState.NeedsReview);
            await Assert.That(outgoing.Transfer.Reason).IsEqualTo(TransferMatchReason.MirrorPair);
            await Assert.That(outgoing.Transfer.DecidedBy).IsEqualTo(TransferDecidedBy.FinTrack);
            await Assert.That(outgoing.Transfer.PartnerTransactionId).IsEqualTo(Scenario.TransactionMappings["mine-in"]);
            await Assert.That(outgoing.Transfer.PartnerDescription).IsEqualTo("Into savings");
            await Assert.That(outgoing.Transfer.PartnerAccountName).IsEqualTo("Savings");
            await Assert.That(outgoing.AccountName).IsEqualTo("Current");
        }
    }

    [Test]
    public async Task GetReviewCountAsync_CountsTheUsersOnly()
    {
        // Arrange
        await SeedUnlabelledPairAsync(Username, "mine");
        await SeedUnlabelledPairAsync(OtherUsername, "theirs");
        await MatchAsync(Username);
        await MatchAsync(OtherUsername);

        // Act
        int count = await BuildSubject(Username).GetReviewCountAsync(CancellationToken);

        // Assert
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task MarkAsTransferAsync_ConfirmsBothHalvesOfASuggestedPair()
    {
        // Arrange
        await SeedUnlabelledPairAsync(Username, "mine");
        await MatchAsync(Username);

        TransfersService subject = BuildSubject(Username);

        // Act
        IReadOnlyList<Transaction> changed =
            await subject.MarkAsTransferAsync(Scenario.TransactionMappings["mine-out"], CancellationToken);

        // Assert
        Dictionary<Guid, BankTransaction> stored = await LoadTransactionsAsync();

        using (Assert.Multiple())
        {
            await Assert.That(changed.Select(static x => x.Transfer!.DecidedBy))
                .IsEquivalentTo([TransferDecidedBy.User, TransferDecidedBy.User]);
            await Assert.That(changed.All(static x => !x.CountsAsIncomeOrSpending)).IsTrue();
            await Assert.That(stored.Values.All(static x => x.Transfer!.Decision == TransferDecision.Transfer))
                .IsTrue();
            await Assert.That(await subject.GetReviewCountAsync(CancellationToken)).IsEqualTo(0);
        }
    }

    [Test]
    public async Task MarkNotATransferAsync_CountsItAndLetsFinTrackDecideTheOtherHalfAgain()
    {
        // Arrange
        await SeedUnlabelledPairAsync(Username, "mine");
        await MatchAsync(Username);

        TransfersService subject = BuildSubject(Username);

        // Act
        IReadOnlyList<Transaction> changed =
            await subject.MarkNotATransferAsync(Scenario.TransactionMappings["mine-out"], CancellationToken);

        // Assert
        Transaction outgoing = changed.Single(x => x.TransactionId == Scenario.TransactionMappings["mine-out"]);
        Transaction incoming = changed.Single(x => x.TransactionId == Scenario.TransactionMappings["mine-in"]);

        using (Assert.Multiple())
        {
            await Assert.That(outgoing.Transfer!.State).IsEqualTo(TransferState.NotATransfer);
            await Assert.That(outgoing.Transfer.DecidedBy).IsEqualTo(TransferDecidedBy.User);
            await Assert.That(outgoing.CountsAsIncomeOrSpending).IsTrue();
            await Assert.That(incoming.Transfer).IsNull();
        }
    }

    [Test]
    public async Task ResetTransferAsync_AfterConfirmingAPair_LetsFinTrackDecideBothAgain()
    {
        // Arrange
        await SeedUnlabelledPairAsync(Username, "mine");
        await MatchAsync(Username);

        TransfersService subject = BuildSubject(Username);
        await subject.MarkAsTransferAsync(Scenario.TransactionMappings["mine-out"], CancellationToken);

        // Act
        IReadOnlyList<Transaction> changed =
            await subject.ResetTransferAsync(Scenario.TransactionMappings["mine-in"], CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changed.Select(static x => x.Transfer!.DecidedBy))
                .IsEquivalentTo([TransferDecidedBy.FinTrack, TransferDecidedBy.FinTrack]);
            await Assert.That(changed.Select(static x => x.Transfer!.State))
                .IsEquivalentTo([TransferState.NeedsReview, TransferState.NeedsReview]);
        }
    }

    [Test]
    public async Task MarkAsTransferAsync_AnotherUsersTransaction_ThrowsAndChangesNothing()
    {
        // Arrange
        await SeedUnlabelledPairAsync(Username, "mine");
        await SeedUnlabelledPairAsync(OtherUsername, "theirs");

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildSubject(Username)
            .MarkAsTransferAsync(Scenario.TransactionMappings["theirs-out"], CancellationToken));

        // Assert
        await Assert.That((await LoadTransactionsAsync()).Values.All(static x => x.Transfer is null)).IsTrue();
    }

    /// <summary>
    /// <c>{prefix}-out</c> from a current account and <c>{prefix}-in</c> into savings, the same amount on the same day
    /// with no transfer label, which FinTrack suggests for review.
    /// </summary>
    private Task<TestingScenario> SeedUnlabelledPairAsync(string username, string prefix) =>
        Scenario.Do(s => s.UserScenario.WithUser(username))
            .Do(s => s.BankConnectionScenario.WithConnection(prefix, username))
            .Do(s => s.BankAccountScenario.WithAccount($"{prefix}-current", prefix, Account("Current")))
            .Do(s => s.BankAccountScenario.WithAccount($"{prefix}-savings", prefix, Account("Savings")))
            .Do(s => s.TransactionScenario.WithTransaction($"{prefix}-out", $"{prefix}-current",
                Details(-20m, "To savings")))
            .Do(s => s.TransactionScenario.WithTransaction($"{prefix}-in", $"{prefix}-savings",
                Details(20m, "Into savings", Yesterday.AddHours(1))));

    private static Func<Guid, Guid, BankAccount> Account(string name) =>
        (userId, connectionId) => new BankAccount(userId, connectionId, $"acc-{name}-{Guid.NewGuid()}", "TRANSACTION",
            name, "GBP");

    private static TransactionDetails Details(decimal amount, string description, DateTime? occurredAt = null) =>
        new TransactionDetailsFaker().Generate() with
        {
            Amount = amount,
            Description = description,
            Type = amount < 0 ? TransactionType.Debit : TransactionType.Credit,
            Currency = "GBP",
            OccurredAt = occurredAt ?? Yesterday,
            IsPending = false,
            LabelledAsTransfer = false,
            Metadata = TransactionMetadata.None
        };

    private Task<TransferMatchSummary> MatchAsync(string username) =>
        new TransferMatchingService(CurrentUserFor(username), DbContextFactory,
            NullLogger<TransferMatchingService>.Instance, TimeProvider.System).MatchAsync(CancellationToken);

    private async Task<Dictionary<Guid, BankTransaction>> LoadTransactionsAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.BankTransactions.ToDictionaryAsync(static x => x.Id, CancellationToken);
    }

    private TransfersService BuildSubject(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<TransfersService>.Instance,
            new TransferMatchingService(CurrentUserFor(username), DbContextFactory,
                NullLogger<TransferMatchingService>.Instance, TimeProvider.System),
            TimeProvider.System);
}

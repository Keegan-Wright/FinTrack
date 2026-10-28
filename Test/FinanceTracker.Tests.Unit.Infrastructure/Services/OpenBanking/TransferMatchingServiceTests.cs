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

    [Test]
    public async Task MatchAsync_RecognisesTheUsersHouseholdMembers()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.HouseholdMemberScenario.WithHouseholdMember("partner", Username))
            .Do(s => s.HouseholdMemberScenario.WithPayeeName("partner", "J Smith"))
            .Do(s => s.HouseholdMemberScenario.WithLinkedAccount("partner", "112233", "12345678"))
            .Do(s => s.TransactionScenario.WithTransaction("by-name", "mine-current",
                Details(300m, description: "J Smith 01JAN26")))
            .Do(s => s.TransactionScenario.WithTransaction("by-account", "mine-current",
                Details(-50m, reference: "112233 12345678")));

        await SeedAccountsAsync(OtherUsername, "theirs")
            .Do(s => s.HouseholdMemberScenario.WithHouseholdMember("their-partner", OtherUsername))
            .Do(s => s.HouseholdMemberScenario.WithPayeeName("their-partner", "Tesco"))
            .Do(s => s.TransactionScenario.WithTransaction("shopping", "mine-current",
                Details(-20m, description: "Tesco")));

        TransferMatchingService subject = BuildSubject(Username);

        // Act
        TransferMatchSummary summary = await subject.MatchAsync(CancellationToken);

        // Assert
        Dictionary<Guid, BankTransaction> stored = (await LoadTransactionsAsync()).ToDictionary(x => x.Id);
        Guid partnerId = Scenario.HouseholdMemberMappings["partner"];

        using (Assert.Multiple())
        {
            await Assert.That(summary).IsEqualTo(new TransferMatchSummary(2, 0, 0));
            await Assert.That(stored[Scenario.TransactionMappings["by-name"]].Transfer!.PartnerHouseholdMemberId)
                .IsEqualTo(partnerId);
            await Assert.That(stored[Scenario.TransactionMappings["by-account"]].Transfer!.PartnerHouseholdMemberId)
                .IsEqualTo(partnerId);
            await Assert.That(stored[Scenario.TransactionMappings["shopping"]].Transfer).IsNull();
        }
    }

    [Test]
    public async Task MatchChangesAsync_FirstRun_MatchesEverything()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.TransactionScenario.WithTransaction("out", "mine-current",
                Details(-50m, labelled: true, occurredAt: DaysAgo(200))))
            .Do(s => s.TransactionScenario.WithTransaction("in", "mine-savings",
                Details(50m, occurredAt: DaysAgo(200))));

        TransferMatchingService subject = BuildSubject(Username);

        // Act
        TransferMatchSummary? summary = await subject.MatchChangesAsync(CancellationToken);

        // Assert
        await Assert.That(summary).IsEqualTo(new TransferMatchSummary(2, 0, 0));
    }

    [Test]
    public async Task MatchChangesAsync_NothingChangedSinceTheLastRun_SkipsIt()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.TransactionScenario.WithTransaction("shopping", "mine-current",
                Details(-20m, occurredAt: DaysAgo(1))));

        TransferMatchingService subject = BuildSubject(Username);
        await subject.MatchChangesAsync(CancellationToken);

        // Act
        TransferMatchSummary? summary = await subject.MatchChangesAsync(CancellationToken);

        // Assert
        await Assert.That(summary).IsNull();
    }

    [Test]
    public async Task MatchChangesAsync_AfterARunThatDecidedSomething_SkipsAsItsDecisionsAreNotChanges()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.TransactionScenario.WithTransaction("out", "mine-current",
                Details(-50m, labelled: true, occurredAt: DaysAgo(1))))
            .Do(s => s.TransactionScenario.WithTransaction("in", "mine-savings",
                Details(50m, occurredAt: DaysAgo(1))));

        TransferMatchingService subject = BuildSubject(Username);
        await subject.MatchChangesAsync(CancellationToken);

        // Act
        TransferMatchSummary? second = await subject.MatchChangesAsync(CancellationToken);

        // Assert
        await Assert.That(second).IsNull();
    }

    [Test]
    public async Task MatchChangesAsync_NewTransaction_PairsWithItsMirror()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.TransactionScenario.WithTransaction("out", "mine-current",
                Details(-50m, labelled: true, occurredAt: DaysAgo(2))));

        TransferMatchingService subject = BuildSubject(Username);
        await subject.MatchChangesAsync(CancellationToken);
        await Scenario.Do(s => s.TransactionScenario.WithTransaction("in", "mine-savings",
            Details(50m, occurredAt: DaysAgo(1))));

        // Act
        await subject.MatchChangesAsync(CancellationToken);

        // Assert
        Dictionary<Guid, BankTransaction> stored = (await LoadTransactionsAsync()).ToDictionary(x => x.Id);

        using (Assert.Multiple())
        {
            await Assert.That(stored[Scenario.TransactionMappings["out"]].Transfer!.PartnerTransactionId)
                .IsEqualTo(Scenario.TransactionMappings["in"]);
            await Assert.That(stored[Scenario.TransactionMappings["in"]].Transfer!.Decision)
                .IsEqualTo(TransferDecision.Transfer);
        }
    }

    [Test]
    public async Task MatchChangesAsync_LeavesUnchangedTransactionsOutsideTheWindowAlone()
    {
        // Arrange
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.TransactionScenario.WithTransaction("old", "mine-current",
                Details(-30m, labelled: true, occurredAt: DaysAgo(100))));

        TransferMatchingService subject = BuildSubject(Username);
        await subject.MatchChangesAsync(CancellationToken);

        // Cleared behind the matcher's back, without touching Updated, so a run that looked at it would decide again
        await ClearTransfersWithoutUpdatingAsync();
        await Scenario.Do(s => s.TransactionScenario.WithTransaction("new", "mine-current",
            Details(-10m, labelled: true, occurredAt: DaysAgo(1))));

        // Act
        await subject.MatchChangesAsync(CancellationToken);

        // Assert
        Dictionary<Guid, BankTransaction> stored = (await LoadTransactionsAsync()).ToDictionary(x => x.Id);

        using (Assert.Multiple())
        {
            await Assert.That(stored[Scenario.TransactionMappings["old"]].Transfer).IsNull();
            await Assert.That(stored[Scenario.TransactionMappings["new"]].Transfer!.Decision)
                .IsEqualTo(TransferDecision.NeedsReview);
        }
    }

    [Test]
    public async Task MatchChangesAsync_KeepsAPairWhoseOtherHalfIsOutsideTheWindow()
    {
        // Arrange, a pair confirmed by the user across more days than the matcher would pair
        await SeedAccountsAsync(Username, "mine")
            .Do(s => s.TransactionScenario.WithTransaction("old", "mine-savings",
                Details(50m, occurredAt: DaysAgo(100))))
            .Do(s => s.TransactionScenario.WithTransaction("recent", "mine-current",
                Details(-50m, occurredAt: DaysAgo(1))));
        await ConfirmPairAsync("old", "recent");

        TransferMatchingService subject = BuildSubject(Username);
        await subject.MatchChangesAsync(CancellationToken);
        await Scenario.Do(s => s.TransactionScenario.WithTransaction("new", "mine-current",
            Details(-10m, occurredAt: DaysAgo(1))));

        // Act
        await subject.MatchChangesAsync(CancellationToken);

        // Assert
        BankTransaction recent = (await LoadTransactionsAsync()).Single(x => x.Id == Scenario.TransactionMappings["recent"]);
        await Assert.That(recent.Transfer!.PartnerTransactionId).IsEqualTo(Scenario.TransactionMappings["old"]);
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

    private static TransactionDetails Details(decimal amount, bool labelled = false, string? reference = null,
        string description = "Description", DateTime? occurredAt = null) =>
        new TransactionDetailsFaker().Generate() with
        {
            Description = description,
            Amount = amount,
            Type = amount < 0 ? TransactionType.Debit : TransactionType.Credit,
            Category = "PURCHASE",
            LabelledAsTransfer = labelled,
            Currency = "GBP",
            OccurredAt = occurredAt ?? Monday,
            IsPending = false,
            Metadata = new TransactionMetadata(reference, null, null, null, null)
        };

    private static DateTime DaysAgo(int days) => DateTime.UtcNow.AddDays(-days);

    /// <summary>
    /// The matcher paired <paramref name="userConfirmed"/> with <paramref name="partner"/> and the user confirmed it,
    /// so <paramref name="partner"/> keeps the matcher's decision but is locked to it.
    /// </summary>
    private async Task ConfirmPairAsync(string userConfirmed, string partner)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Dictionary<Guid, BankTransaction> transactions =
            await context.BankTransactions.ToDictionaryAsync(x => x.Id, CancellationToken);
        BankTransaction confirmed = transactions[Scenario.TransactionMappings[userConfirmed]];
        BankTransaction other = transactions[Scenario.TransactionMappings[partner]];

        confirmed.DecideTransfer(TransferInfo.Matched(TransferReason.MirrorPair, other.Id, other.AccountId, Monday));
        confirmed.MarkAsTransfer(Monday);
        other.DecideTransfer(TransferInfo.Matched(TransferReason.MirrorPair, confirmed.Id, confirmed.AccountId, Monday));
        await context.SaveChangesAsync(CancellationToken);
    }

    private async Task ClearTransfersWithoutUpdatingAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE \"BankTransactions\" SET \"Transfer_Decision\" = NULL, \"Transfer_Source\" = NULL, " +
            "\"Transfer_Reason\" = NULL, \"Transfer_PartnerTransactionId\" = NULL, " +
            "\"Transfer_PartnerAccountId\" = NULL, \"Transfer_PartnerHouseholdMemberId\" = NULL, " +
            "\"Transfer_DecidedAt\" = NULL", CancellationToken);
    }

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

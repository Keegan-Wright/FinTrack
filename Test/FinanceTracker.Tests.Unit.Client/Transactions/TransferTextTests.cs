using FinanceTracker.Client.Components.Transactions;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Tests.Unit.Client.Transactions;

public class TransferTextTests
{
    private static readonly DateTime October4 = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    [Arguments(TransferState.Transfer, "Rainy Day", null, "Transfer · Rainy Day")]
    [Arguments(TransferState.Transfer, null, "Jane Smith", "Transfer · Jane Smith")]
    [Arguments(TransferState.Transfer, null, null, "Transfer")]
    [Arguments(TransferState.NeedsReview, "Rainy Day", null, "Review transfer")]
    [Arguments(TransferState.NotATransfer, null, null, "Not a transfer")]
    public async Task Label_SaysTheStateAndWhoWith(TransferState state, string? account, string? member,
        string expected)
    {
        // Act
        string label = TransferText.Label(Transfer(state, account: account, member: member, time: October4));

        // Assert
        await Assert.That(label).IsEqualTo(expected);
    }

    [Test]
    public async Task Partner_WithTheDate_PrefersTheHouseholdMember()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        string? account = TransferText.Partner(Transfer(TransferState.Transfer, account: "Rainy Day", time: October4));
        string? member = TransferText.Partner(Transfer(TransferState.Transfer, account: "Joint", member: "Jane Smith"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(account).IsEqualTo("Rainy Day, 4 Oct");
            await Assert.That(member).IsEqualTo("Jane Smith");
        }
    }

    [Test]
    public async Task OtherHalf_IsTheDescriptionAndDate()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        string? otherHalf =
            TransferText.OtherHalf(Transfer(TransferState.Transfer, description: "To savings", time: October4));

        // Assert
        await Assert.That(otherHalf).IsEqualTo("To savings · 4 Oct 2026");
    }

    [Test]
    [Arguments(TransferState.Transfer, TransferDecidedBy.FinTrack, new[] { TransferAction.NotATransfer })]
    [Arguments(TransferState.Transfer, TransferDecidedBy.User,
        new[] { TransferAction.NotATransfer, TransferAction.LetFinTrackDecide })]
    [Arguments(TransferState.NeedsReview, TransferDecidedBy.FinTrack,
        new[] { TransferAction.MarkAsTransfer, TransferAction.NotATransfer })]
    [Arguments(TransferState.NotATransfer, TransferDecidedBy.User,
        new[] { TransferAction.MarkAsTransfer, TransferAction.LetFinTrackDecide })]
    public async Task Actions_OfferOnlyWhatWouldChangeSomething(TransferState state, TransferDecidedBy decidedBy,
        TransferAction[] expected)
    {
        // Act
        List<TransferAction> actions = TransferText.Actions(Transfer(state, decidedBy)).ToList();

        // Assert
        await Assert.That(actions).IsEquivalentTo(expected);
    }

    [Test]
    public async Task Actions_NoDecision_OffersMarkingItATransfer()
    {
        // Act
        List<TransferAction> actions = TransferText.Actions(null).ToList();

        // Assert
        await Assert.That(actions).IsEquivalentTo([TransferAction.MarkAsTransfer]);
    }

    [Test]
    public async Task Reason_UsersDecision_SaysTheyMadeIt()
    {
        // Act
        string reason = TransferText.Reason(Transfer(TransferState.NotATransfer, TransferDecidedBy.User));

        // Assert
        await Assert.That(reason).IsEqualTo("You said this counts as income or spending.");
    }

    [Test]
    [Arguments("Cafes", "Decided by your rule Cafes.")]
    [Arguments(null, "Decided by one of your rules.")]
    public async Task Reason_RulesDecision_NamesTheRule(string? ruleName, string expected)
    {
        // Arrange
        TransactionTransfer transfer = new()
        {
            State = TransferState.NotATransfer,
            DecidedBy = TransferDecidedBy.Rule,
            RuleName = ruleName
        };

        // Act
        string reason = TransferText.Reason(transfer);

        // Assert
        await Assert.That(reason).IsEqualTo(expected);
    }

    [Test]
    public async Task Reason_CoversEveryMatchReason()
    {
        // Act
        List<string> reasons = Enum.GetValues<TransferMatchReason>()
            .Select(static x => TransferText.Reason(Transfer(TransferState.NeedsReview, reason: x)))
            .ToList();

        // Assert
        await Assert.That(reasons).DoesNotContain("Decided by a rule.");
    }

    [Test]
    public async Task MissingHalf_SaysWhyThereIsNoOtherHalf()
    {
        // Act
        string paired = TransferText.MissingHalf(Transfer(TransferState.NeedsReview, description: "To savings"));
        string ambiguous = TransferText.MissingHalf(Transfer(TransferState.NeedsReview,
            reason: TransferMatchReason.AmbiguousMirrorPair));
        string missing = TransferText.MissingHalf(Transfer(TransferState.NeedsReview,
            reason: TransferMatchReason.BankTransferLabel));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(paired).IsEqualTo("Paired with To savings");
            await Assert.That(ambiguous).IsEqualTo("Several transactions could be the other half");
            await Assert.That(missing).IsEqualTo("The other half isn't on any of your accounts");
        }
    }

    [Test]
    [Arguments(null, -5, "PURCHASE", "Spending")]
    [Arguments(null, 5, "CREDIT", "Income")]
    [Arguments(null, 5, "PURCHASE", "Refund")]
    [Arguments(TransferState.NotATransfer, 5, "CREDIT", "Income")]
    [Arguments(TransferState.Transfer, -5, "PURCHASE", "Transfer")]
    [Arguments(TransferState.NeedsReview, -5, "PURCHASE", "Review")]
    public async Task CountsAs_SaysHowItCounts(TransferState? state, int amount, string category, string expected)
    {
        // Arrange
        Transaction transaction = TransactionListTests.Transaction("Description", amount,
            transfer: state is { } known ? Transfer(known) : null, category: category);

        // Act
        string countsAs = TransferText.CountsAs(transaction);

        // Assert
        await Assert.That(countsAs).IsEqualTo(expected);
    }

    private static TransactionTransfer Transfer(TransferState state,
        TransferDecidedBy decidedBy = TransferDecidedBy.FinTrack,
        TransferMatchReason reason = TransferMatchReason.MirrorPair, string? account = null, string? member = null,
        DateTime? time = null, string? description = null) => new()
    {
        State = state,
        DecidedBy = decidedBy,
        Reason = reason,
        PartnerAccountName = account,
        PartnerHouseholdMemberName = member,
        PartnerTime = time,
        PartnerDescription = description
    };
}

using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Household;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;

namespace FinanceTracker.Tests.Unit.Domain.OpenBanking.Transfers;

[Category("Domain")]
public class TransferMatcherTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime Monday = new(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DecidedAt = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

    private readonly BankAccount _current = Account("Current", "111111", "11111111");
    private readonly BankAccount _savings = Account("Savings", "222222", "22222222");
    private readonly BankAccount _joint = Account("Joint", "333333", "33333333");
    private readonly BankAccount _card = new(UserId, Guid.NewGuid(), "ext-card", "CREDIT_CARD", "Credit Card", "GBP",
        BankAccountKind.Card);
    private readonly HouseholdMember _partner = Member("444444", "44444444", "Jane Smith", "J Smith");

    [Test]
    public async Task Match_ReferenceNamesOwnAccount_PairsWithItsMirror()
    {
        BankTransaction outgoing = Transaction(_current, -50m, Monday, reference: "222222 22222222");
        BankTransaction incoming = Transaction(_savings, 50m, Monday.AddDays(1));

        TransferMatchSummary summary = Match(outgoing, incoming);

        using (Assert.Multiple())
        {
            await Assert.That(outgoing.Transfer).IsEqualTo(
                TransferInfo.Matched(TransferReason.OwnAccountReference, incoming.Id, _savings.Id, DecidedAt));
            await Assert.That(incoming.Transfer).IsEqualTo(
                TransferInfo.Matched(TransferReason.OwnAccountReference, outgoing.Id, _current.Id, DecidedAt));
            await Assert.That(outgoing.CountsAsIncomeOrSpending).IsFalse();
            await Assert.That(incoming.CountsAsIncomeOrSpending).IsFalse();
            await Assert.That(summary).IsEqualTo(new TransferMatchSummary(2, 0, 0));
        }
    }

    [Test]
    public async Task Match_ReferenceNamesOwnAccountWithoutAMirror_IsATransferToThatAccount()
    {
        BankTransaction outgoing = Transaction(_current, -50m, Monday, reference: "222222 22222222");

        Match(outgoing);

        await Assert.That(outgoing.Transfer).IsEqualTo(
            TransferInfo.Matched(TransferReason.OwnAccountReference, null, _savings.Id, DecidedAt));
    }

    [Test]
    public async Task Match_ReferenceNamesOwnAccount_MirrorLaterArrives_IsPairedOnTheNextRun()
    {
        BankTransaction outgoing = Transaction(_current, -50m, Monday, reference: "222222 22222222");
        Match(outgoing);
        BankTransaction incoming = Transaction(_savings, 50m, Monday.AddDays(2));

        Match(outgoing, incoming);

        using (Assert.Multiple())
        {
            await Assert.That(outgoing.Transfer!.PartnerTransactionId).IsEqualTo(incoming.Id);
            await Assert.That(incoming.Transfer!.PartnerTransactionId).IsEqualTo(outgoing.Id);
        }
    }

    [Test]
    public async Task Match_ReferenceNamesSeveralAccounts_IsLeftForTheMirrorPair()
    {
        // A pot reporting the same sort code and number as its parent
        BankAccount pot = Account("Pot", "222222", "22222222");
        BankTransaction outgoing = Transaction(_current, -50m, Monday, labelled: true,
            reference: "222222 22222222");
        BankTransaction incoming = Transaction(pot, 50m, Monday.AddHours(1));

        TransferMatcher.Match([_current, _savings, pot], [], [outgoing, incoming], DecidedAt);

        using (Assert.Multiple())
        {
            await Assert.That(outgoing.Transfer).IsEqualTo(
                TransferInfo.Matched(TransferReason.MirrorPair, incoming.Id, pot.Id, DecidedAt));
            await Assert.That(incoming.Transfer).IsEqualTo(
                TransferInfo.Matched(TransferReason.MirrorPair, outgoing.Id, _current.Id, DecidedAt));
        }
    }

    [Test]
    public async Task Match_ReferenceNamesSomeoneElsesAccount_IsLeftForTheLabel()
    {
        BankTransaction payment = Transaction(_joint, -49.2m, Monday, reference: "999999 99999999",
            labelled: true);

        Match(payment);

        await Assert.That(payment.Transfer).IsEqualTo(
            TransferInfo.ToReview(TransferReason.BankTransferLabel, null, null, DecidedAt));
    }

    [Test]
    public async Task Match_LabelledMirrorPair_IsATransfer()
    {
        BankTransaction outgoing = Transaction(_current, -0.45m, Monday, labelled: true);
        BankTransaction incoming = Transaction(_savings, 0.45m, Monday.AddMinutes(1));

        Match(outgoing, incoming);

        using (Assert.Multiple())
        {
            await Assert.That(outgoing.Transfer).IsEqualTo(
                TransferInfo.Matched(TransferReason.MirrorPair, incoming.Id, _savings.Id, DecidedAt));
            await Assert.That(incoming.Transfer).IsEqualTo(
                TransferInfo.Matched(TransferReason.MirrorPair, outgoing.Id, _current.Id, DecidedAt));
        }
    }

    [Test]
    public async Task Match_UnlabelledMirrorPair_IsUpForReviewWithTheSuggestedPartner()
    {
        BankTransaction outgoing = Transaction(_current, -20m, Monday);
        BankTransaction incoming = Transaction(_savings, 20m, Monday.AddHours(3));

        Match(outgoing, incoming);

        using (Assert.Multiple())
        {
            await Assert.That(outgoing.Transfer).IsEqualTo(
                TransferInfo.ToReview(TransferReason.MirrorPair, incoming.Id, _savings.Id, DecidedAt));
            await Assert.That(outgoing.CountsAsIncomeOrSpending).IsTrue();
            await Assert.That(incoming.CountsAsIncomeOrSpending).IsTrue();
        }
    }

    [Test]
    [Arguments(2, true)]
    [Arguments(3, false)]
    public async Task Match_MirrorPair_OnlyWithinTwoDays(int daysApart, bool paired)
    {
        // Date only, as Lloyds sends them
        BankTransaction outgoing = Transaction(_current, -50m, Monday.Date, labelled: true);
        BankTransaction incoming = Transaction(_savings, 50m, Monday.Date.AddDays(daysApart).AddHours(23));

        Match(outgoing, incoming);

        await Assert.That(incoming.Transfer is not null).IsEqualTo(paired);
    }

    [Test]
    public async Task Match_MirrorOnTheSameAccount_IsNotPaired()
    {
        BankTransaction outgoing = Transaction(_current, -50m, Monday, labelled: true);
        BankTransaction refund = Transaction(_current, 50m, Monday.AddHours(1));

        Match(outgoing, refund);

        using (Assert.Multiple())
        {
            await Assert.That(outgoing.Transfer!.Reason).IsEqualTo(TransferReason.BankTransferLabel);
            await Assert.That(refund.Transfer).IsNull();
        }
    }

    [Test]
    public async Task Match_MirrorInAnotherCurrency_IsNotPaired()
    {
        BankTransaction outgoing = Transaction(_current, -50m, Monday, labelled: true);
        BankTransaction incoming = Transaction(_savings, 50m, Monday, currency: "EUR");

        Match(outgoing, incoming);

        await Assert.That(incoming.Transfer).IsNull();
    }

    [Test]
    public async Task Match_SeveralMirrors_PairsTheClosest()
    {
        BankTransaction outgoing = Transaction(_current, -50m, Monday, labelled: true);
        BankTransaction closer = Transaction(_savings, 50m, Monday.AddHours(1));
        BankTransaction further = Transaction(_joint, 50m, Monday.AddDays(1));

        Match(outgoing, closer, further);

        using (Assert.Multiple())
        {
            await Assert.That(outgoing.Transfer!.PartnerTransactionId).IsEqualTo(closer.Id);
            await Assert.That(further.Transfer).IsNull();
        }
    }

    [Test]
    public async Task Match_EquallyCloseMirrors_AreAllUpForReview()
    {
        BankTransaction outgoing = Transaction(_current, -50m, Monday, labelled: true);
        BankTransaction first = Transaction(_savings, 50m, Monday.AddHours(1));
        BankTransaction second = Transaction(_joint, 50m, Monday.AddHours(1));

        Match(outgoing, first, second);

        TransferInfo ambiguous = TransferInfo.ToReview(TransferReason.AmbiguousMirrorPair, null, null, DecidedAt);
        using (Assert.Multiple())
        {
            await Assert.That(outgoing.Transfer).IsEqualTo(ambiguous);
            await Assert.That(first.Transfer).IsEqualTo(ambiguous);
            await Assert.That(second.Transfer).IsEqualTo(ambiguous);
        }
    }

    [Test]
    public async Task Match_IncomingCloserToAnotherOutgoing_PairsWithThatOne()
    {
        BankTransaction earlier = Transaction(_current, -50m, Monday, labelled: true);
        BankTransaction later = Transaction(_joint, -50m, Monday.AddDays(1), labelled: true);
        BankTransaction incoming = Transaction(_savings, 50m, Monday.AddDays(1).AddMinutes(5));

        Match(earlier, later, incoming);

        using (Assert.Multiple())
        {
            await Assert.That(incoming.Transfer!.PartnerTransactionId).IsEqualTo(later.Id);
            await Assert.That(earlier.Transfer!.Reason).IsEqualTo(TransferReason.BankTransferLabel);
        }
    }

    [Test]
    public async Task Match_EachTransactionIsInOnePairOnly()
    {
        BankTransaction outgoing = Transaction(_current, -50m, Monday, reference: "222222 22222222");
        BankTransaction incoming = Transaction(_savings, 50m, Monday);
        BankTransaction secondOutgoing = Transaction(_joint, -50m, Monday.AddMinutes(1), labelled: true);

        Match(outgoing, incoming, secondOutgoing);

        using (Assert.Multiple())
        {
            await Assert.That(incoming.Transfer!.PartnerTransactionId).IsEqualTo(outgoing.Id);
            await Assert.That(secondOutgoing.Transfer!.Reason).IsEqualTo(TransferReason.BankTransferLabel);
        }
    }

    [Test]
    public async Task Match_LabelledWithoutAPartner_IsUpForReviewAndStillCounted()
    {
        BankTransaction pot = Transaction(_current, -0.45m, Monday, labelled: true);

        Match(pot);

        using (Assert.Multiple())
        {
            await Assert.That(pot.Transfer).IsEqualTo(
                TransferInfo.ToReview(TransferReason.BankTransferLabel, null, null, DecidedAt));
            await Assert.That(pot.CountsAsIncomeOrSpending).IsTrue();
        }
    }

    [Test]
    public async Task Match_PendingTransactions_AreSkipped()
    {
        BankTransaction outgoing = Transaction(_current, -50m, Monday, labelled: true, pending: true);
        BankTransaction incoming = Transaction(_savings, 50m, Monday);

        Match(outgoing, incoming);

        using (Assert.Multiple())
        {
            await Assert.That(outgoing.Transfer).IsNull();
            await Assert.That(incoming.Transfer).IsNull();
        }
    }

    [Test]
    public async Task Match_UsersDecision_IsKeptAndNeverPaired()
    {
        BankTransaction outgoing = Transaction(_current, -50m, Monday, labelled: true);
        BankTransaction incoming = Transaction(_savings, 50m, Monday);
        outgoing.MarkNotATransfer(Monday);

        Match(outgoing, incoming);

        using (Assert.Multiple())
        {
            await Assert.That(outgoing.Transfer!.Decision).IsEqualTo(TransferDecision.NotATransfer);
            await Assert.That(outgoing.Transfer.Source).IsEqualTo(DecisionSource.User);
            await Assert.That(incoming.Transfer).IsNull();
        }
    }

    [Test]
    public async Task Match_PartnerOfAUsersDecision_IsLeftAlone()
    {
        BankTransaction outgoing = Transaction(_current, -50m, Monday, labelled: true);
        BankTransaction incoming = Transaction(_savings, 50m, Monday);
        Match(outgoing, incoming);
        outgoing.MarkAsTransfer(DecidedAt);
        TransferInfo partnersDecision = incoming.Transfer!;

        TransferMatchSummary summary = Match(outgoing, incoming);

        using (Assert.Multiple())
        {
            await Assert.That(incoming.Transfer).IsEqualTo(partnersDecision);
            await Assert.That(summary).IsEqualTo(new TransferMatchSummary(0, 0, 0));
        }
    }

    [Test]
    public async Task Match_RunAgain_ChangesNothing()
    {
        BankTransaction outgoing = Transaction(_current, -50m, Monday, reference: "222222 22222222");
        BankTransaction incoming = Transaction(_savings, 50m, Monday);
        BankTransaction labelled = Transaction(_joint, -10m, Monday, labelled: true);
        Match(outgoing, incoming, labelled);
        TransferInfo?[] before = [outgoing.Transfer, incoming.Transfer, labelled.Transfer];

        TransferMatchSummary summary = TransferMatcher.Match([_current, _savings, _joint], [_partner],
            [outgoing, incoming, labelled], DecidedAt.AddDays(1));

        using (Assert.Multiple())
        {
            await Assert.That(summary).IsEqualTo(new TransferMatchSummary(0, 0, 0));
            await Assert.That(new[] { outgoing.Transfer, incoming.Transfer, labelled.Transfer })
                .IsEquivalentTo(before);
        }
    }

    [Test]
    public async Task Match_PartnerNoLongerThere_ClearsTheMatchersDecision()
    {
        BankTransaction outgoing = Transaction(_current, -20m, Monday);
        BankTransaction incoming = Transaction(_savings, 20m, Monday);
        Match(outgoing, incoming);

        TransferMatchSummary summary = Match(outgoing);

        using (Assert.Multiple())
        {
            await Assert.That(outgoing.Transfer).IsNull();
            await Assert.That(summary).IsEqualTo(new TransferMatchSummary(0, 0, 1));
        }
    }

    [Test]
    public async Task Match_RuleDecision_IsNotReplaced()
    {
        BankTransaction outgoing = Transaction(_current, -50m, Monday, labelled: true);
        TransferInfo ruled = new(TransferDecision.NotATransfer, DecisionSource.Rule, null, null, null, null, Monday);
        outgoing.DecideTransfer(ruled);

        Match(outgoing, Transaction(_savings, 50m, Monday));

        await Assert.That(outgoing.Transfer).IsEqualTo(ruled);
    }

    [Test]
    [Arguments("Jane Smith", null)]
    [Arguments("J Smith 01JAN26", null)]
    [Arguments("Bills", "444444 44444444")]
    public async Task Match_RecognisesAHouseholdMember_IsATransferToThem(string description, string? reference)
    {
        BankTransaction payment = Transaction(_joint, -300m, Monday, reference: reference, description: description);

        TransferMatchSummary summary = Match(payment);

        using (Assert.Multiple())
        {
            await Assert.That(payment.Transfer).IsEqualTo(TransferInfo.WithHouseholdMember(_partner.Id, DecidedAt));
            await Assert.That(payment.CountsAsIncomeOrSpending).IsFalse();
            await Assert.That(summary).IsEqualTo(new TransferMatchSummary(1, 0, 0));
        }
    }

    [Test]
    public async Task Match_HouseholdMemberLabelledATransfer_IsStillTheirsNotUpForReview()
    {
        BankTransaction payment = Transaction(_joint, 300m, Monday, labelled: true, description: "Jane Smith");

        Match(payment);

        await Assert.That(payment.Transfer).IsEqualTo(TransferInfo.WithHouseholdMember(_partner.Id, DecidedAt));
    }

    [Test]
    public async Task Match_HouseholdMemberAndAMirrorPair_PrefersThePair()
    {
        BankTransaction outgoing = Transaction(_current, -50m, Monday, labelled: true, description: "J Smith");
        BankTransaction incoming = Transaction(_savings, 50m, Monday.AddMinutes(1));

        Match(outgoing, incoming);

        await Assert.That(outgoing.Transfer).IsEqualTo(
            TransferInfo.Matched(TransferReason.MirrorPair, incoming.Id, _savings.Id, DecidedAt));
    }

    [Test]
    public async Task Match_SeveralHouseholdMembersRecogniseIt_IsLeftForTheLabel()
    {
        HouseholdMember namesake = Member("555555", "55555555", "J Smith");
        BankTransaction payment = Transaction(_joint, -300m, Monday, labelled: true, description: "J Smith");

        TransferMatcher.Match([_current, _savings, _joint], [_partner, namesake], [payment], DecidedAt);

        await Assert.That(payment.Transfer).IsEqualTo(
            TransferInfo.ToReview(TransferReason.BankTransferLabel, null, null, DecidedAt));
    }

    [Test]
    public async Task Match_HouseholdMemberRemoved_ClearsTheMatchersDecision()
    {
        BankTransaction payment = Transaction(_joint, -300m, Monday, description: "Jane Smith");
        Match(payment);

        TransferMatchSummary summary = TransferMatcher.Match([_current, _savings, _joint], [], [payment], DecidedAt);

        using (Assert.Multiple())
        {
            await Assert.That(payment.Transfer).IsNull();
            await Assert.That(summary).IsEqualTo(new TransferMatchSummary(0, 0, 1));
        }
    }

    [Test]
    public async Task Match_RepaymentOntoACard_IsATransferWithoutALabel()
    {
        BankTransaction payment = Transaction(_current, -150m, Monday);
        BankTransaction repayment = Transaction(_card, 150m, Monday.AddDays(1));

        Match(payment, repayment);

        using (Assert.Multiple())
        {
            await Assert.That(payment.Transfer).IsEqualTo(
                TransferInfo.Matched(TransferReason.MirrorPair, repayment.Id, _card.Id, DecidedAt));
            await Assert.That(repayment.Transfer).IsEqualTo(
                TransferInfo.Matched(TransferReason.MirrorPair, payment.Id, _current.Id, DecidedAt));
        }
    }

    [Test]
    public async Task Match_SpendingOnACardMirroringMoneyIn_IsOnlySuggested()
    {
        // Paying for something on the card while someone pays you the same amount, so not certain
        BankTransaction purchase = Transaction(_card, -20m, Monday);
        BankTransaction moneyIn = Transaction(_current, 20m, Monday);

        Match(purchase, moneyIn);

        await Assert.That(purchase.Transfer).IsEqualTo(
            TransferInfo.ToReview(TransferReason.MirrorPair, moneyIn.Id, _current.Id, DecidedAt));
    }

    private TransferMatchSummary Match(params BankTransaction[] transactions) =>
        TransferMatcher.Match([_current, _savings, _joint, _card], [_partner], transactions, DecidedAt);

    private static BankAccount Account(string name, string sortCode, string number)
    {
        BankAccount account = new(UserId, Guid.NewGuid(), $"ext-{name}", "TRANSACTION", name, "GBP");
        account.UpdateIdentifier(AccountIdentifier.Create(sortCode, number, null));
        return account;
    }

    private static HouseholdMember Member(string sortCode, string number, params string[] payeeNames)
    {
        HouseholdMember member = new(UserId, "Jane", "Smith", null);
        member.LinkAccount(AccountIdentifier.Create(sortCode, number, null)!);
        foreach (string payeeName in payeeNames)
        {
            member.AddPayeeName(payeeName);
        }

        return member;
    }

    private static BankTransaction Transaction(BankAccount account, decimal amount, DateTime occurredAt,
        bool labelled = false, string? reference = null, string currency = "GBP", bool pending = false,
        string description = "Description") =>
        BankTransaction.Import(account, new TransactionDetails($"tx-{Guid.NewGuid()}", description,
            amount < 0 ? TransactionType.Debit : TransactionType.Credit, "PURCHASE", amount, currency, occurredAt,
            pending, [], new TransactionMetadata(reference, null, null, null, null), labelled));
}

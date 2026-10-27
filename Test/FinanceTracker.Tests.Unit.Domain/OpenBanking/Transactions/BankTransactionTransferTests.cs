using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;

namespace FinanceTracker.Tests.Unit.Domain.OpenBanking.Transactions;

[Category("Domain")]
public class BankTransactionTransferTests
{
    private static readonly DateTime At = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task BankTransaction_Undecided_CountsAsIncomeOrSpending()
    {
        BankTransaction transaction = Build();

        using (Assert.Multiple())
        {
            await Assert.That(transaction.Transfer).IsNull();
            await Assert.That(transaction.CountsAsIncomeOrSpending).IsTrue();
        }
    }

    [Test]
    [Arguments(TransferDecision.Transfer, false)]
    [Arguments(TransferDecision.NotATransfer, true)]
    [Arguments(TransferDecision.NeedsReview, true)]
    public async Task BankTransaction_CountsAsIncomeOrSpending_UnlessATransfer(TransferDecision decision,
        bool counts)
    {
        BankTransaction transaction = Build();

        transaction.DecideTransfer(new TransferInfo(decision, DecisionSource.Matcher, null, null, null, null, At));

        await Assert.That(transaction.CountsAsIncomeOrSpending).IsEqualTo(counts);
    }

    [Test]
    [Arguments(DecisionSource.Matcher, DecisionSource.Matcher, true)]
    [Arguments(DecisionSource.Matcher, DecisionSource.Rule, true)]
    [Arguments(DecisionSource.Rule, DecisionSource.User, true)]
    [Arguments(DecisionSource.Rule, DecisionSource.Matcher, false)]
    [Arguments(DecisionSource.User, DecisionSource.Rule, false)]
    public async Task BankTransaction_DecideTransfer_OnlyReplacesAnEqualOrLowerSource(DecisionSource existing,
        DecisionSource incoming, bool replaced)
    {
        BankTransaction transaction = Build();
        transaction.DecideTransfer(Info(existing));
        TransferInfo decision = Info(incoming) with { Decision = TransferDecision.NotATransfer };

        bool recorded = transaction.DecideTransfer(decision);

        using (Assert.Multiple())
        {
            await Assert.That(recorded).IsEqualTo(replaced);
            await Assert.That(transaction.Transfer!.Source).IsEqualTo(replaced ? incoming : existing);
        }
    }

    [Test]
    public async Task BankTransaction_DecideTransfer_ItselfAsPartner_Throws()
    {
        BankTransaction transaction = Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.FromResult(
            transaction.DecideTransfer(Info(DecisionSource.Matcher) with { PartnerTransactionId = transaction.Id })));
    }

    [Test]
    [Arguments(DecisionSource.Matcher, DecisionSource.Matcher, true)]
    [Arguments(DecisionSource.User, DecisionSource.Matcher, false)]
    [Arguments(DecisionSource.User, DecisionSource.User, true)]
    public async Task BankTransaction_ClearTransfer_OnlyClearsAnEqualOrLowerSource(DecisionSource existing,
        DecisionSource clearing, bool cleared)
    {
        BankTransaction transaction = Build();
        transaction.DecideTransfer(Info(existing));

        bool result = transaction.ClearTransfer(clearing);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsEqualTo(cleared);
            await Assert.That(transaction.Transfer is null).IsEqualTo(cleared);
        }
    }

    [Test]
    public async Task BankTransaction_MarkAsTransfer_KeepsTheMatchedPartner()
    {
        BankTransaction transaction = Build();
        Guid partnerId = Guid.NewGuid();
        Guid partnerAccountId = Guid.NewGuid();
        transaction.DecideTransfer(
            TransferInfo.ToReview(TransferReason.MirrorPair, partnerId, partnerAccountId, At.AddDays(-1)));

        transaction.MarkAsTransfer(At);

        await Assert.That(transaction.Transfer).IsEqualTo(new TransferInfo(TransferDecision.Transfer,
            DecisionSource.User, TransferReason.MirrorPair, partnerId, partnerAccountId, null, At));
    }

    [Test]
    public async Task BankTransaction_MarkNotATransfer_DropsThePartner()
    {
        BankTransaction transaction = Build();
        transaction.DecideTransfer(TransferInfo.Matched(TransferReason.MirrorPair, Guid.NewGuid(), Guid.NewGuid(),
            At.AddDays(-1)));

        transaction.MarkNotATransfer(At);

        using (Assert.Multiple())
        {
            await Assert.That(transaction.Transfer).IsEqualTo(new TransferInfo(TransferDecision.NotATransfer,
                DecisionSource.User, null, null, null, null, At));
            await Assert.That(transaction.CountsAsIncomeOrSpending).IsTrue();
        }
    }

    [Test]
    public async Task TransferInfo_IsSameAs_IgnoresWhenItWasDecided()
    {
        TransferInfo decision = Info(DecisionSource.Matcher);

        using (Assert.Multiple())
        {
            await Assert.That(decision.IsSameAs(decision with { DecidedAt = At.AddDays(1) })).IsTrue();
            await Assert.That(decision.IsSameAs(decision with { Decision = TransferDecision.NeedsReview })).IsFalse();
            await Assert.That(decision.IsSameAs(null)).IsFalse();
        }
    }

    private static TransferInfo Info(DecisionSource source) =>
        new(TransferDecision.Transfer, source, null, null, null, null, At);

    private static BankTransaction Build() =>
        BankTransaction.Import(new BankAccount(Guid.NewGuid(), Guid.NewGuid(), "acc-1", "TRANSACTION", "Main", "GBP"),
            new TransactionDetails("tx-1", "Coffee", TransactionType.Debit, "PURCHASE", -3.5m, "GBP", At, false, [],
                TransactionMetadata.None, false));
}

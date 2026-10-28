using FinanceTracker.Client.Components.Review;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Tests.Unit.Client.Review;

public class ReviewPairsTests
{
    private readonly Guid _outId = Guid.NewGuid();
    private readonly Guid _inId = Guid.NewGuid();

    [Test]
    public async Task Group_APairWaitingTogether_ShowsOnceWithTheMoneyOutFirst()
    {
        // Arrange
        Transaction outgoing = Transaction(_outId, -20m, partner: _inId);
        Transaction incoming = Transaction(_inId, 20m, partner: _outId);

        // Act
        List<ReviewPair> pairs = ReviewPairs.Group([incoming, outgoing]);

        // Assert
        await Assert.That(pairs).IsEquivalentTo([new ReviewPair(outgoing, incoming)]);
    }

    [Test]
    public async Task Group_NoOtherHalfWaiting_ShowsItAlone()
    {
        // Arrange
        Transaction labelled = Transaction(_outId, -30m);

        // Act
        List<ReviewPair> pairs = ReviewPairs.Group([labelled]);

        // Assert
        await Assert.That(pairs).IsEquivalentTo([new ReviewPair(labelled, null)]);
    }

    [Test]
    public async Task Group_OtherHalfPointsElsewhere_ShowsEachAlone()
    {
        // Arrange
        Transaction first = Transaction(_outId, -20m, partner: _inId);
        Transaction second = Transaction(_inId, 20m, partner: Guid.NewGuid());

        // Act
        List<ReviewPair> pairs = ReviewPairs.Group([first, second]);

        // Assert
        await Assert.That(pairs).IsEquivalentTo([new ReviewPair(first, null), new ReviewPair(second, null)]);
    }

    [Test]
    public async Task Group_KeepsTheListsOrder()
    {
        // Arrange
        Transaction newest = Transaction(Guid.NewGuid(), -5m);
        Transaction incoming = Transaction(_inId, 20m, partner: _outId);
        Transaction outgoing = Transaction(_outId, -20m, partner: _inId);
        Transaction oldest = Transaction(Guid.NewGuid(), -1m);

        // Act
        List<ReviewPair> pairs = ReviewPairs.Group([newest, incoming, outgoing, oldest]);

        // Assert
        await Assert.That(pairs.Select(static x => x.First)).IsEquivalentTo([newest, outgoing, oldest]);
    }

    private static Transaction Transaction(Guid id, decimal amount, Guid? partner = null) => new()
    {
        TransactionId = id,
        Description = "Description",
        TransactionType = amount < 0 ? "Debit" : "Credit",
        TransactionCategory = "TRANSFER",
        Amount = amount,
        Currency = "GBP",
        TransactionTime = DateTime.UtcNow,
        Pending = false,
        Tags = [],
        Transfer = new TransactionTransfer
        {
            State = TransferState.NeedsReview,
            DecidedBy = TransferDecidedBy.FinTrack,
            Reason = TransferMatchReason.MirrorPair,
            PartnerTransactionId = partner
        }
    };
}

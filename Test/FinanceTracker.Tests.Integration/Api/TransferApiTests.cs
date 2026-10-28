using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Tests.Integration.Http;

namespace FinanceTracker.Tests.Integration.Api;

public class TransferApiTests : IntegrationTestBase
{
    [Test]
    public async Task Review_WithNoTransactions_IsEmpty()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        ITransfersService service = ApiFor(user).Get<ITransfersService>();

        // Act
        List<Transaction> toReview = await service.GetTransfersToReviewAsync(CancellationToken)
            .ToListAsync(CancellationToken);
        int count = await service.GetReviewCountAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(toReview).IsEmpty();
            await Assert.That(count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Decisions_OnAMissingTransaction_AreNotFound()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        ITransfersService service = ApiFor(user).Get<ITransfersService>();
        Guid missing = Guid.NewGuid();

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.MarkAsTransferAsync(missing, CancellationToken));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.MarkNotATransferAsync(missing, CancellationToken));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ResetTransferAsync(missing, CancellationToken));
        }
    }
}

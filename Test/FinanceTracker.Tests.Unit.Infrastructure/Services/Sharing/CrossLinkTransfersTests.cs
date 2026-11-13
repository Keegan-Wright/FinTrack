using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Infrastructure.Transactions;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationKind = FinanceTracker.Domain.Notifications.NotificationKind;
using Transaction = FinanceTracker.Contracts.Transactions.Transaction;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

/// <summary>
/// Money moved between two linked people's accounts. Alex pays £50 from their current account into Sam's Joint,
/// which Sam shares with Alex. Alex doesn't share their current account.
/// </summary>
public class CrossLinkTransfersTests : InfrastructureTestBase
{
    private const string Alex = "alex";
    private const string Sam = "sam";

    private static readonly DateTime Paid = DateTime.UtcNow.AddDays(-2);

    private ServiceProvider _services = null!;
    private Guid _linkId;

    [Before(Test)]
    public async Task SeedAsync()
    {
        List<(Guid UserId, NotificationKind Kind, string Title)> raised = [];
        ServiceCollection services = new();
        services.AddCurrentUser();
        services.AddScoped<INotifier>(sp => new RecordingNotifier(sp.GetRequiredService<ICurrentUser>(), raised));
        services.AddTransferMatching(DbContextFactory);
        _services = services.BuildServiceProvider();

        await Scenario.Do(s => s.UserScenario.WithUser("Alex", "Smith", "alex@example.test", Alex))
            .Do(s => s.UserScenario.WithUser("Sam", "Taylor", "sam@example.test", Sam))
            .Do(s => s.BankConnectionScenario.WithConnection("alex-bank", Alex))
            .Do(s => s.BankAccountScenario.WithAccount("alex-current", "alex-bank", Named("Alex current")))
            .Do(s => s.BankConnectionScenario.WithConnection("sam-bank", Sam))
            .Do(s => s.BankAccountScenario.WithAccount("sam-joint", "sam-bank", Named("Joint")));

        InviteOutcome sent = await Sharing(Sam).InviteAsync(new InviteToShare { Who = Alex }, CancellationToken);
        await Sharing(Alex).AcceptAsync(sent.Link!.Id, CancellationToken);
        _linkId = sent.Link.Id;
    }

    [After(Test)]
    public async Task TearDownAsync() => await _services.DisposeAsync();

    [Test]
    public async Task NothingShared_StaysSpendingAndIncome()
    {
        // Arrange
        await SeedPaymentAsync(labelled: true);

        // Act
        await MatchAsync(Alex);
        await MatchAsync(Sam);

        // Assert, each half only up for review on the bank's say so, as any payment to someone else
        using (Assert.Multiple())
        {
            await Assert.That((await LoadAsync("alex-out")).Transfer!.PartnerTransactionId).IsNull();
            await Assert.That((await LoadAsync("joint-in")).Transfer!.PartnerTransactionId).IsNull();
            await Assert.That((await LoadAsync("joint-in")).Transfer!.Reason)
                .IsEqualTo(TransferReason.BankTransferLabel);
        }
    }

    [Test]
    public async Task SharingTheJointAccount_PairsTheHalves_NeitherCountsAsSpendingOrIncome()
    {
        // Arrange
        await SeedPaymentAsync(labelled: true);

        // Act
        await ShareJointAsync();

        // Assert
        BankTransaction outgoing = await LoadAsync("alex-out");
        BankTransaction incoming = await LoadAsync("joint-in");
        using (Assert.Multiple())
        {
            await Assert.That(outgoing.Transfer!.Decision).IsEqualTo(TransferDecision.Transfer);
            await Assert.That(outgoing.Transfer.PartnerTransactionId).IsEqualTo(incoming.Id);
            await Assert.That(incoming.Transfer!.PartnerTransactionId).IsEqualTo(outgoing.Id);
            await Assert.That(outgoing.CountsAsIncomeOrSpending).IsFalse();
            await Assert.That(incoming.CountsAsIncomeOrSpending).IsFalse();
        }
    }

    [Test]
    public async Task EachSees_WhoseAccountTheOtherHalfIsOn_NotWhatTheyKeepPrivate()
    {
        // Arrange
        await SeedPaymentAsync(labelled: true);
        await ShareJointAsync();

        // Act
        List<Transaction> asAlex = await SeenBy(Alex);
        Transaction samsOwn = (await SeenBy(Sam)).Single(x => x.TransactionId == Id("joint-in"));

        // Assert
        Transaction alexsOwn = asAlex.Single(x => x.TransactionId == Id("alex-out"));
        Transaction intoJoint = asAlex.Single(x => x.TransactionId == Id("joint-in"));
        using (Assert.Multiple())
        {
            await Assert.That(alexsOwn.Transfer!.PartnerAccountName).IsEqualTo("Joint");
            await Assert.That(alexsOwn.Transfer.PartnerOwnerName).IsEqualTo("Sam Taylor");

            // Money from Alex's own account, so not hidden as a private transfer
            await Assert.That(intoJoint.Description).IsEqualTo("From Alex");
            await Assert.That(intoJoint.Transfer!.PartnerOwnerName).IsNull();

            await Assert.That(samsOwn.Transfer!.PartnerOwnerName).IsEqualTo("Alex Smith");
            await Assert.That(samsOwn.Transfer.PartnerAccountName).IsNull();
            await Assert.That(samsOwn.Transfer.PartnerDescription).IsNull();
        }
    }

    [Test]
    public async Task MatchingAgainAsEither_KeepsThePair()
    {
        // Arrange
        await SeedPaymentAsync(labelled: true);
        await ShareJointAsync();

        // Act
        TransferMatchSummary asSam = await MatchAsync(Sam);
        TransferMatchSummary asAlex = await MatchAsync(Alex);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(asSam).IsEqualTo(new TransferMatchSummary(0, 0, 0));
            await Assert.That(asAlex).IsEqualTo(new TransferMatchSummary(0, 0, 0));
            await Assert.That((await LoadAsync("joint-in")).Transfer!.PartnerTransactionId).IsEqualTo(Id("alex-out"));
        }
    }

    [Test]
    public async Task AnUnlabelledPair_IsUpForReviewByBoth_AndEitherConfirmingConfirmsBoth()
    {
        // Arrange
        await SeedPaymentAsync(labelled: false);
        await ShareJointAsync();
        List<Transaction> samToReview = await Transfers(Sam).GetTransfersToReviewAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Act
        await Transfers(Alex).MarkAsTransferAsync(Id("alex-out"), CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(samToReview.Select(static x => x.Transfer!.PartnerOwnerName ?? string.Empty))
                .IsEquivalentTo(["Alex Smith"]);
            await Assert.That((await LoadAsync("joint-in")).Transfer!.Decision).IsEqualTo(TransferDecision.Transfer);
            await Assert.That(await Transfers(Sam).GetReviewCountAsync(CancellationToken)).IsEqualTo(0);
        }
    }

    [Test]
    public async Task UnsharingTheJointAccount_UndoesThePair_EvenOneConfirmed()
    {
        // Arrange
        await SeedPaymentAsync(labelled: false);
        await ShareJointAsync();
        await Transfers(Alex).MarkAsTransferAsync(Id("alex-out"), CancellationToken);

        // Act
        await Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares { Accounts = [] },
            CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((await LoadAsync("alex-out")).Transfer).IsNull();
            await Assert.That((await LoadAsync("joint-in")).Transfer).IsNull();
        }
    }

    [Test]
    public async Task EndingTheLink_UndoesThePair()
    {
        // Arrange
        await SeedPaymentAsync(labelled: true);
        await ShareJointAsync();

        // Act
        await Sharing(Alex).EndAsync(_linkId, CancellationToken);

        // Assert, back to each half alone
        using (Assert.Multiple())
        {
            await Assert.That((await LoadAsync("alex-out")).Transfer!.PartnerTransactionId).IsNull();
            await Assert.That((await LoadAsync("joint-in")).Transfer!.PartnerTransactionId).IsNull();
        }
    }

    private async Task SeedPaymentAsync(bool labelled) =>
        await Scenario
            .Do(s => s.TransactionScenario.WithTransaction("alex-out", "alex-current",
                Details(-50m, "To Sam joint", labelled)))
            .Do(s => s.TransactionScenario.WithTransaction("joint-in", "sam-joint",
                Details(50m, "From Alex", labelled)));

    private Task<LinkShares> ShareJointAsync() =>
        Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts =
            [
                new AccountShareChoice { AccountId = Scenario.AccountMappings["sam-joint"], Mode = ShareMode.ReadOnly }
            ]
        }, CancellationToken);

    private Task<TransferMatchSummary> MatchAsync(string username) =>
        new TransferMatchingService(CurrentUserFor(username), DbContextFactory,
                NullLogger<TransferMatchingService>.Instance, TimeProvider.System, TestMetrics.Unobserved)
            .MatchAsync(CancellationToken);

    private Task<List<Transaction>> SeenBy(string username) =>
        new TransactionsService(CurrentUserFor(username), DbContextFactory, NullLogger<TransactionsService>.Instance,
                TimeProvider.System, new AccountScope(CurrentUserFor(username)))
            .GetAllTransactionsAsync(new FilterTransactions(), CancellationToken)
            .ToListAsync(CancellationToken).AsTask();

    private TransfersService Transfers(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<TransfersService>.Instance,
            new TransferMatchingService(CurrentUserFor(username), DbContextFactory,
                NullLogger<TransferMatchingService>.Instance, TimeProvider.System, TestMetrics.Unobserved),
            TimeProvider.System, new AccountScope(CurrentUserFor(username)),
            _services.GetRequiredService<IServiceScopeFactory>());

    private async Task<BankTransaction> LoadAsync(string transaction)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid id = Id(transaction);
        return await context.BankTransactions.AsNoTracking().SingleAsync(x => x.Id == id, CancellationToken);
    }

    private Guid Id(string transaction) => Scenario.TransactionMappings[transaction];

    private static Func<Guid, Guid, BankAccount> Named(string name) => (userId, connectionId) =>
        new BankAccount(userId, connectionId, $"ext-{name}", "TRANSACTION", name, "GBP");

    private static TransactionDetails Details(decimal amount, string description, bool labelled) =>
        new TransactionDetailsFaker().Generate() with
        {
            Amount = amount,
            Description = description,
            Type = amount < 0 ? TransactionType.Debit : TransactionType.Credit,
            Currency = "GBP",
            OccurredAt = Paid,
            IsPending = false,
            LabelledAsTransfer = labelled,
            Metadata = TransactionMetadata.None
        };

    private SharingService Sharing(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SharingService>.Instance,
            _services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
}

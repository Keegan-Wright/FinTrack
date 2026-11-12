using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Bills;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Sharing;
using FinanceTracker.Infrastructure.Transactions;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using DomainBill = FinanceTracker.Domain.Bills.Bill;
using DomainBillFrequency = FinanceTracker.Domain.Bills.BillFrequency;
using DomainBillSource = FinanceTracker.Domain.Bills.BillSource;
using NotificationKind = FinanceTracker.Domain.Notifications.NotificationKind;
using Transaction = FinanceTracker.Contracts.Transactions.Transaction;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

/// <summary>
/// Sam's bills seen by Alex. Rent is paid from Joint, which Sam shares with Alex, Gifts from Gift fund, which Sam keeps
/// private. Alex has a coffee on their own account.
/// </summary>
public class BillSharingTests : InfrastructureTestBase
{
    private const string Alex = "alex";
    private const string Sam = "sam";

    private static readonly DateTime Spent = DateTime.UtcNow.AddDays(-2);

    private ServiceProvider _services = null!;
    private Guid _linkId;
    private Guid _rentId;
    private Guid _giftsId;

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
            .Do(s => s.TransactionScenario.WithTransaction("alex-coffee", "alex-current", Details("COFFEE")))
            .Do(s => s.BankConnectionScenario.WithConnection("sam-bank", Sam))
            .Do(s => s.BankAccountScenario.WithAccount("sam-joint", "sam-bank", Named("Joint")))
            .Do(s => s.BankAccountScenario.WithAccount("sam-private", "sam-bank", Named("Gift fund")));

        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            Guid samId = Scenario.UserMappings[Sam];
            DateOnly start = DateOnly.FromDateTime(Spent).AddMonths(-1);
            DomainBill rent = new(samId, Account("sam-joint"), "Rent", "LANDLORD", DomainBillFrequency.Monthly, 900m,
                start, DomainBillSource.Manual);
            DomainBill gifts = new(samId, Account("sam-private"), "Gifts", "SHOP", DomainBillFrequency.Monthly, 20m,
                start, DomainBillSource.Manual);
            context.Bills.AddRange(rent, gifts);
            await context.SaveChangesAsync(CancellationToken);
            _rentId = rent.Id;
            _giftsId = gifts.Id;
        }

        InviteOutcome sent = await Sharing(Sam).InviteAsync(new InviteToShare { Who = Alex }, CancellationToken);
        await Sharing(Alex).AcceptAsync(sent.Link!.Id, CancellationToken);
        _linkId = sent.Link.Id;
        await ShareJointAsync(ShareMode.ReadOnly);
    }

    [After(Test)]
    public async Task TearDownAsync() => await _services.DisposeAsync();

    [Test]
    public async Task ABillOnASharedAccount_GoesWithIt()
    {
        // Act
        List<Bill> alexs = await Bills(Alex).GetBillsAsync(CancellationToken).ToListAsync(CancellationToken);
        LinkShares sams = (await Sharing(Sam).GetSharesAsync(_linkId, CancellationToken))!;
        LinkShares asAlex = (await Sharing(Alex).GetSharesAsync(_linkId, CancellationToken))!;

        // Assert
        Bill rent = alexs.Single();
        using (Assert.Multiple())
        {
            await Assert.That((rent.Name, rent.SharedBy, rent.CanChange, rent.CanDelete, rent.AccountName))
                .IsEqualTo(("Rent", (string?)"Sam Taylor", false, false, "Joint"));
            await Assert.That(sams.YourItems.Where(static x => x.Kind == SharedItemKind.Bill)
                    .Select(static x => (x.Name, x.FollowsAccount, x.Mode)))
                .IsEquivalentTo([("Rent", (string?)"Joint", (ShareMode?)ShareMode.ReadOnly), ("Gifts", null, null)]);
            await Assert.That(asAlex.TheirItems.Select(static x => (x.Kind, x.Name)))
                .IsEquivalentTo([(SharedItemKind.Bill, "Rent")]);
        }
    }

    [Test]
    public async Task ReadOnly_AlexCantChangeIt()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Bills(Alex).UpdateBillAsync(_rentId, Save("Big rent", Account("sam-joint")), CancellationToken));
    }

    [Test]
    public async Task AccountSharedWithChanges_AlexEditsTheBill_ButCantMoveOrDeleteIt()
    {
        // Arrange
        await ShareJointAsync(ShareMode.AllowChanges);

        // Act
        Bill saved = await Bills(Alex).UpdateBillAsync(_rentId, Save("Big rent", Account("sam-joint")),
            CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((saved.Name, saved.SharedBy, saved.CanChange))
                .IsEqualTo(("Big rent", (string?)"Sam Taylor", true));
            await Assert.That((await Bills(Sam).GetBillAsync(_rentId, CancellationToken)).Name).IsEqualTo("Big rent");
            await Assert.ThrowsAsync<InvalidOperationException>(() => Bills(Alex)
                .UpdateBillAsync(_rentId, Save("Big rent", Account("alex-current")), CancellationToken));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Bills(Alex).DeleteBillAsync(_rentId, CancellationToken));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Bills(Alex).EndBillAsync(_giftsId, CancellationToken));
        }
    }

    [Test]
    public async Task ABillChosenOnItsOwn_IsShared_ThoughItsAccountIsnt()
    {
        // Act
        await Sharing(Sam).ChangeItemSharesAsync(_linkId, new ChangeItemShares
        {
            Items = [new ItemShareChoice { Kind = SharedItemKind.Bill, ItemId = _giftsId, Mode = ShareMode.ReadOnly }]
        }, CancellationToken);

        // Assert
        List<Bill> alexs = await Bills(Alex).GetBillsAsync(CancellationToken).ToListAsync(CancellationToken);
        await Assert.That(alexs.Select(static x => (x.Name, x.AccountName)))
            .IsEquivalentTo([("Gifts", "A private account"), ("Rent", "Joint")]);
    }

    [Test]
    public async Task ChoosingABillThatGoesWithItsAccount_DoesNothing()
    {
        // Act
        await Sharing(Sam).ChangeItemSharesAsync(_linkId, new ChangeItemShares
        {
            Items = [new ItemShareChoice { Kind = SharedItemKind.Bill, ItemId = _rentId, Mode = ShareMode.ReadOnly }]
        }, CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await Assert.That(await context.Shares.CountAsync(x => x.ItemId == _rentId, CancellationToken)).IsEqualTo(0);
    }

    [Test]
    public async Task AlexSaysTheirCoffeePaysSamsRent_ItShowsByName()
    {
        // Act
        Transaction paid = await Bills(Alex).PayBillAsync(Transaction("alex-coffee"), new PayBill { BillId = _rentId },
            CancellationToken);

        // Assert
        await Assert.That(paid.Bill?.BillName).IsEqualTo("Rent");
    }

    [Test]
    public async Task UnsharingTheAccount_TakesItsBillOffAlexsTransactions()
    {
        // Arrange
        await Bills(Alex).PayBillAsync(Transaction("alex-coffee"), new PayBill { BillId = _rentId },
            CancellationToken);

        // Act
        await Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares { Accounts = [] },
            CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(await Bills(Alex).GetBillsAsync(CancellationToken).ToListAsync(CancellationToken))
                .IsEmpty();
            await Assert.That((await LoadAsync("alex-coffee")).BillPayment).IsNull();
        }
    }

    [Test]
    public async Task JustMine_LeavesOutBillsChosenForThem()
    {
        // Arrange
        await Sharing(Sam).ChangeItemSharesAsync(_linkId, new ChangeItemShares
        {
            Items = [new ItemShareChoice { Kind = SharedItemKind.Bill, ItemId = _giftsId, Mode = ShareMode.ReadOnly }]
        }, CancellationToken);
        await Sharing(Alex).ChangeViewAsync(new ChangeSharingView { IncludeShared = false }, CancellationToken);

        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        AccountsInView justMine = await Scope(Alex).ResolveAsync(context, CancellationToken);
        List<VisibleBill> bills = await SharedBills.ForAsync(context, Scenario.UserMappings[Alex], justMine,
            CancellationToken);

        // Assert
        await Assert.That(bills).IsEmpty();
    }

    private Task<LinkShares> ShareJointAsync(ShareMode mode) =>
        Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts = [new AccountShareChoice { AccountId = Account("sam-joint"), Mode = mode }]
        }, CancellationToken);

    private static SaveBill Save(string name, Guid accountId) => new()
    {
        Name = name,
        Payee = "LANDLORD",
        AccountId = accountId,
        Frequency = BillFrequency.Monthly,
        ExpectedAmount = 900m,
        NextDue = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10),
        TolerancePercent = 10m
    };

    private async Task<BankTransaction> LoadAsync(string transaction)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid id = Transaction(transaction);
        return await context.BankTransactions.AsNoTracking().SingleAsync(x => x.Id == id, CancellationToken);
    }

    private static Func<Guid, Guid, BankAccount> Named(string name) => (userId, connectionId) =>
        new BankAccount(userId, connectionId, $"ext-{name}", "TRANSACTION", name, "GBP");

    private static TransactionDetails Details(string description) =>
        new TransactionDetailsFaker().Generate() with
        {
            Amount = -3m,
            Description = description,
            Type = TransactionType.Debit,
            Currency = "GBP",
            OccurredAt = Spent,
            IsPending = false,
            LabelledAsTransfer = false,
            Metadata = TransactionMetadata.None
        };

    private Guid Account(string name) => Scenario.AccountMappings[name];

    private Guid Transaction(string name) => Scenario.TransactionMappings[name];

    private AccountScope Scope(string username) => new(CurrentUserFor(username));

    private BillsService Bills(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<BillsService>.Instance,
            new BillSyncService(CurrentUserFor(username), DbContextFactory, NullLogger<BillSyncService>.Instance,
                TimeProvider.System, TestMetrics.Unobserved), TimeProvider.System, Scope(username));

    private SharingService Sharing(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SharingService>.Instance,
            _services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
}

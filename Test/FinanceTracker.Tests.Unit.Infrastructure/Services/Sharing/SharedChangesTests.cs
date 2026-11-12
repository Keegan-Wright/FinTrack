using FinanceTracker.Contracts.Account;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Account;
using FinanceTracker.Infrastructure.Bills;
using FinanceTracker.Infrastructure.Classification;
using FinanceTracker.Infrastructure.Debts;
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
using DomainBill = FinanceTracker.Domain.Bills.Bill;
using DomainBillFrequency = FinanceTracker.Domain.Bills.BillFrequency;
using DomainBillSource = FinanceTracker.Domain.Bills.BillSource;
using DomainDebt = FinanceTracker.Domain.Debts.Debt;
using NotificationKind = FinanceTracker.Domain.Notifications.NotificationKind;
using Transaction = FinanceTracker.Contracts.Transactions.Transaction;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

/// <summary>
/// Changing transactions on an account shared with changes allowed. Sam shares Joint with Alex, letting them change
/// it, and keeps Gift fund private. Alex has their own Food tag, Rent bill and Loan debt, Sam their own Groceries tag.
/// </summary>
public class SharedChangesTests : InfrastructureTestBase
{
    private const string Alex = "alex";
    private const string Sam = "sam";
    private const string Jo = "jo";

    private static readonly DateTime Spent = DateTime.UtcNow.AddDays(-2);

    private ServiceProvider _services = null!;
    private Guid _linkId;
    private Guid _rentId;
    private Guid _loanId;

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
            .Do(s => s.UserScenario.WithUser(Jo))
            .Do(s => s.BankConnectionScenario.WithConnection("alex-bank", Alex))
            .Do(s => s.BankAccountScenario.WithAccount("alex-current", "alex-bank", Named("Alex current")))
            .Do(s => s.ClassificationScenario.WithClassification("food", Alex, "Food"))
            .Do(s => s.BankConnectionScenario.WithConnection("sam-bank", Sam))
            .Do(s => s.BankAccountScenario.WithAccount("sam-joint", "sam-bank", Named("Joint")))
            .Do(s => s.BankAccountScenario.WithAccount("sam-private", "sam-bank", Named("Gift fund")))
            .Do(s => s.TransactionScenario.WithTransaction("sam-shop", "sam-joint", Details(-40m, "Supermarket")))
            .Do(s => s.ClassificationScenario.WithClassification("groceries", Sam, "Groceries"))
            .Do(s => s.BankConnectionScenario.WithConnection("jo-bank", Jo))
            .Do(s => s.BankAccountScenario.WithAccount("jo-current", "jo-bank", Named("Jo current")))
            .Do(s => s.TransactionScenario.WithTransaction("jo-shop", "jo-current", Details(-10m, "Corner shop")));

        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            Guid alexId = Scenario.UserMappings[Alex];
            DomainBill rent = new(alexId, Account("alex-current"), "Rent", "LANDLORD", DomainBillFrequency.Monthly,
                40m, DateOnly.FromDateTime(Spent).AddMonths(-1), DomainBillSource.Manual);
            DomainDebt loan = new(alexId, "Loan", null, 1_000m, DateOnly.FromDateTime(Spent).AddMonths(-1), 0m, 40m);
            context.Bills.Add(rent);
            context.Debts.Add(loan);
            await context.SaveChangesAsync(CancellationToken);
            _rentId = rent.Id;
            _loanId = loan.Id;
        }

        InviteOutcome sent = await Sharing(Sam).InviteAsync(new InviteToShare { Who = Alex }, CancellationToken);
        await Sharing(Alex).AcceptAsync(sent.Link!.Id, CancellationToken);
        _linkId = sent.Link.Id;
        await ShareJointAsync(ShareMode.AllowChanges);
    }

    [After(Test)]
    public async Task TearDownAsync() => await _services.DisposeAsync();

    [Test]
    public async Task Tagging_ChangesAllowed_BothSeeTheTagByName()
    {
        // Act
        await TagAsync(Alex, "sam-shop", "food");

        // Assert
        Transaction asAlex = await SeenBy(Alex, "sam-shop");
        Transaction asSam = await SeenBy(Sam, "sam-shop");
        using (Assert.Multiple())
        {
            await Assert.That(asAlex.CustomTags).IsEquivalentTo(["Food"]);
            await Assert.That(asAlex.IsReadOnly).IsFalse();
            await Assert.That(asSam.CustomTags).IsEquivalentTo(["Food"]);
            await Assert.That(asSam.IsReadOnly).IsFalse();
        }
    }

    [Test]
    public async Task Tagging_SamsOwnTagAndAlexsTogether_SamCanFilterByEither()
    {
        // Arrange
        await TagAsync(Sam, "sam-shop", "groceries");
        await TagAsync(Alex, "sam-shop", "food");

        // Act
        List<string> filters = (await Transactions(Sam).GetTagsForTransactionFiltersAsync(CancellationToken)
            .ToListAsync(CancellationToken)).Select(static x => x.Tag).ToList();
        Transaction asSam = await SeenBy(Sam, "sam-shop");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(asSam.CustomTags).IsEquivalentTo(["Groceries", "Food"]);
            await Assert.That(filters).Contains("Food");
        }
    }

    [Test]
    public async Task Tagging_ReadOnly_IsRefused()
    {
        // Arrange
        await ShareJointAsync(ShareMode.ReadOnly);

        // Act and assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => TagAsync(Alex, "sam-shop", "food"));
        await Assert.That((await SeenBy(Alex, "sam-shop")).IsReadOnly).IsTrue();
    }

    [Test]
    public async Task Tagging_SomeoneNotLinked_IsRefused() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => TagAsync(Alex, "jo-shop", "food"));

    [Test]
    public async Task Tagging_WithAnotherPersonsTag_IsIgnored()
    {
        // Act, Sam's tag isn't Alex's to use
        await TagAsync(Alex, "sam-shop", "groceries");

        // Assert
        await Assert.That((await SeenBy(Sam, "sam-shop")).CustomTags).IsEmpty();
    }

    [Test]
    public async Task Splitting_KeepsSamsTagAndAddsAlexs()
    {
        // Arrange
        await TagAsync(Sam, "sam-shop", "groceries");

        // Act
        Transaction split = await Transactions(Alex).SplitAsync(Transaction("sam-shop"), new SplitTransaction
        {
            Parts =
            [
                new SplitTransactionPart { Amount = -30m, TagId = Tag("groceries") },
                new SplitTransactionPart { Amount = -10m, TagId = Tag("food") }
            ]
        }, CancellationToken);

        // Assert
        Transaction asSam = await SeenBy(Sam, "sam-shop");
        using (Assert.Multiple())
        {
            await Assert.That(split.Splits.Select(static x => x.Tag ?? string.Empty))
                .IsEquivalentTo(["Groceries", "Food"]);
            await Assert.That(asSam.Splits.Select(static x => x.Tag ?? string.Empty))
                .IsEquivalentTo(["Groceries", "Food"]);
        }
    }

    [Test]
    public async Task PayingABill_SamSeesItsName_AndItCountsOnAlexsBill()
    {
        // Act
        Transaction paid = await Bills(Alex).PayBillAsync(Transaction("sam-shop"), new PayBill { BillId = _rentId },
            CancellationToken);

        // Assert
        Bill rent = await Bills(Alex).GetBillAsync(_rentId, CancellationToken);
        Transaction asSam = await SeenBy(Sam, "sam-shop");
        using (Assert.Multiple())
        {
            await Assert.That(paid.Bill!.BillName).IsEqualTo("Rent");
            await Assert.That(paid.Bill.OwnerName).IsNull();
            await Assert.That(asSam.Bill!.BillName).IsEqualTo("Rent");
            await Assert.That(asSam.Bill.OwnerName).IsEqualTo("Alex Smith");
            await Assert.That(rent.LastPaid).IsEqualTo(40m);
        }
    }

    [Test]
    public async Task PayingADebt_SamSeesItsName()
    {
        // Act
        await Debts(Alex).PayDebtAsync(Transaction("sam-shop"), new PayDebt { DebtId = _loanId }, CancellationToken);

        // Assert
        TransactionDebt debt = (await SeenBy(Sam, "sam-shop")).Debt!;
        using (Assert.Multiple())
        {
            await Assert.That(debt.DebtName).IsEqualTo("Loan");
            await Assert.That(debt.OwnerName).IsEqualTo("Alex Smith");
        }
    }

    [Test]
    public async Task PayingAnotherPersonsBillNotSharedWithThem_IsRefused()
    {
        // Arrange, paid from Sam's private account, so not shared with Alex
        Guid samsBill;
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            DomainBill bill = new(Scenario.UserMappings[Sam], Account("sam-private"), "Energy", "ENERGY",
                DomainBillFrequency.Monthly, 40m, DateOnly.FromDateTime(Spent), DomainBillSource.Manual);
            context.Bills.Add(bill);
            await context.SaveChangesAsync(CancellationToken);
            samsBill = bill.Id;
        }

        // Act and assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => Bills(Alex).PayBillAsync(Transaction("sam-shop"),
            new PayBill { BillId = samsBill }, CancellationToken));
    }

    [Test]
    public async Task EndingTheLink_TakesAlexsTagBillAndDebtOffSamsTransaction()
    {
        // Arrange
        await TagAsync(Sam, "sam-shop", "groceries");
        await TagAsync(Alex, "sam-shop", "food");
        await Bills(Alex).PayBillAsync(Transaction("sam-shop"), new PayBill { BillId = _rentId }, CancellationToken);

        // Act
        await Sharing(Sam).EndAsync(_linkId, CancellationToken);

        // Assert
        BankTransaction stored = await LoadAsync("sam-shop");
        Transaction asSam = await SeenBy(Sam, "sam-shop");
        using (Assert.Multiple())
        {
            await Assert.That(stored.CustomClassificationIds).IsEquivalentTo([Tag("groceries")]);
            await Assert.That(stored.BillPayment).IsNull();
            await Assert.That(asSam.CustomTags).IsEquivalentTo(["Groceries"]);
        }
    }

    [Test]
    public async Task DeletingAlexsTag_TakesItOffSamsTransaction()
    {
        // Arrange
        await TagAsync(Alex, "sam-shop", "food");

        // Act
        await Classifications(Alex).RemoveCustomClassificationAsync(Tag("food"), CancellationToken);

        // Assert
        await Assert.That((await LoadAsync("sam-shop")).CustomClassificationIds).IsEmpty();
    }

    [Test]
    public async Task TransferToAPrivateAccount_HidesTheBanksDescriptionNotTheAmount()
    {
        // Arrange
        await SeedTransferToGiftFundAsync();

        // Act
        List<Transaction> asAlex = await AllSeenBy(Alex);
        List<Transaction> found = await Transactions(Alex)
            .GetAllTransactionsAsync(new FilterTransactions { SearchTerm = "gift" }, CancellationToken)
            .ToListAsync(CancellationToken);
        List<AccountAndTransactions> accounts = await new AccountService(CurrentUserFor(Alex), DbContextFactory,
                NullLogger<AccountService>.Instance, Scope(Alex))
            .GetAccountsAndMostRecentTransactionsAsync(10, CancellationToken)
            .ToListAsync(CancellationToken);
        Transaction asSam = await SeenBy(Sam, "sam-to-gift");

        // Assert
        Transaction transfer = asAlex.Single(x => x.TransactionId == Transaction("sam-to-gift"));
        using (Assert.Multiple())
        {
            await Assert.That(transfer.Description).IsEqualTo(PrivateTransfers.ToPrivateAccount);
            await Assert.That(transfer.Amount).IsEqualTo(-25m);
            await Assert.That(transfer.Transfer!.PartnerDescription).IsNull();
            await Assert.That(transfer.Transfer.PartnerAccountName).IsNull();
            await Assert.That(asAlex.Select(static x => x.Description)).DoesNotContain("For the gift fund");
            await Assert.That(found).IsEmpty();
            await Assert.That(accounts.SelectMany(static x => x.Transactions!).Select(static x => x.Description))
                .DoesNotContain("For the gift fund");
            await Assert.That(asSam.Description).IsEqualTo("For the gift fund");
        }
    }

    [Test]
    public async Task TransferWithNoOtherHalfFound_WhileTheyKeepAnAccountPrivate_HidesTheDescription()
    {
        // Arrange, the bank calls it a transfer but the other half hasn't synced, it may be into Gift fund
        await SeedUnpairedTransferAsync();

        // Act
        Transaction asAlex = await SeenBy(Alex, "sam-unpaired");
        List<Transaction> found = await Transactions(Alex)
            .GetAllTransactionsAsync(new FilterTransactions { SearchTerm = "savings" }, CancellationToken)
            .ToListAsync(CancellationToken);
        Transaction asSam = await SeenBy(Sam, "sam-unpaired");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(asAlex.Description).IsEqualTo(PrivateTransfers.MaybeToPrivateAccount);
            await Assert.That(asAlex.Amount).IsEqualTo(-15m);
            await Assert.That(found).IsEmpty();
            await Assert.That(asSam.Description).IsEqualTo("Birthday savings");
        }
    }

    [Test]
    public async Task TransferWithNoOtherHalfFound_WhenEveryAccountIsShared_ShowsTheDescription()
    {
        // Arrange, nothing of Sam's is hidden from Alex, so it can't have gone anywhere private
        await SeedUnpairedTransferAsync();
        await Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts =
            [
                new AccountShareChoice { AccountId = Account("sam-joint"), Mode = ShareMode.AllowChanges },
                new AccountShareChoice { AccountId = Account("sam-private"), Mode = ShareMode.ReadOnly }
            ]
        }, CancellationToken);

        // Act and assert
        await Assert.That((await SeenBy(Alex, "sam-unpaired")).Description).IsEqualTo("Birthday savings");
    }

    [Test]
    public async Task MarkingNotATransfer_OnASharedHalf_ShowsTheirChangeAndNothingPrivate()
    {
        // Arrange
        await SeedTransferToGiftFundAsync();
        TransfersService subject = new(CurrentUserFor(Alex), DbContextFactory, NullLogger<TransfersService>.Instance,
            new TransferMatchingService(CurrentUserFor(Alex), DbContextFactory,
                NullLogger<TransferMatchingService>.Instance, TimeProvider.System, TestMetrics.Unobserved),
            TimeProvider.System, Scope(Alex), _services.GetRequiredService<IServiceScopeFactory>());

        // Act
        IReadOnlyList<Transaction> changed =
            await subject.MarkNotATransferAsync(Transaction("sam-to-gift"), CancellationToken);

        // Assert
        BankTransaction stored = await LoadAsync("sam-to-gift");
        using (Assert.Multiple())
        {
            await Assert.That(changed.Select(static x => x.TransactionId)).IsEquivalentTo([Transaction("sam-to-gift")]);
            await Assert.That(changed.Single().Description).IsEqualTo("For the gift fund");
            await Assert.That(stored.Transfer!.Decision).IsEqualTo(TransferDecision.NotATransfer);
        }
    }

    /// <summary>
    /// Sam moves money from Joint to Gift fund, matched as a pair between their own accounts.
    /// </summary>
    private async Task SeedTransferToGiftFundAsync()
    {
        await Scenario
            .Do(s => s.TransactionScenario.WithTransaction("sam-to-gift", "sam-joint", Details(-25m, "For the gift fund")))
            .Do(s => s.TransactionScenario.WithTransaction("gift-in", "sam-private", Details(25m, "From joint")));

        await new TransferMatchingService(CurrentUserFor(Sam), DbContextFactory,
            NullLogger<TransferMatchingService>.Instance, TimeProvider.System, TestMetrics.Unobserved)
            .MatchAsync(CancellationToken);

        BankTransaction matched = await LoadAsync("sam-to-gift");
        if (matched.Transfer?.PartnerAccountId != Account("sam-private"))
        {
            throw new InvalidOperationException("The transfer to the gift fund wasn't matched.");
        }
    }

    /// <summary>
    /// Money out of Joint the bank calls a transfer, with no other half on any of Sam's accounts.
    /// </summary>
    private async Task SeedUnpairedTransferAsync()
    {
        await Scenario.Do(s => s.TransactionScenario.WithTransaction("sam-unpaired", "sam-joint",
            Details(-15m, "Birthday savings") with { LabelledAsTransfer = true }));

        await new TransferMatchingService(CurrentUserFor(Sam), DbContextFactory,
                NullLogger<TransferMatchingService>.Instance, TimeProvider.System, TestMetrics.Unobserved)
            .MatchAsync(CancellationToken);
    }

    private Task<LinkShares> ShareJointAsync(ShareMode mode) =>
        Sharing(Sam).ChangeAccountSharesAsync(_linkId, new ChangeAccountShares
        {
            Accounts = [new AccountShareChoice { AccountId = Account("sam-joint"), Mode = mode }]
        }, CancellationToken);

    private Task TagAsync(string username, string transaction, string tag) =>
        Classifications(username).AddCustomClassificationsToTransactionAsync(new AddCustomClassificationsToTransaction
        {
            TransactionId = Transaction(transaction),
            Classifications = [new SelectedCustomClassification { ClassificationId = Tag(tag) }]
        }, CancellationToken);

    private async Task<Transaction> SeenBy(string username, string transaction)
    {
        Guid id = Transaction(transaction);
        return (await AllSeenBy(username)).Single(x => x.TransactionId == id);
    }

    private Task<List<Transaction>> AllSeenBy(string username) =>
        Transactions(username).GetAllTransactionsAsync(new FilterTransactions(), CancellationToken)
            .ToListAsync(CancellationToken).AsTask();

    private async Task<BankTransaction> LoadAsync(string transaction)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        Guid id = Transaction(transaction);
        return await context.BankTransactions.AsNoTracking().SingleAsync(x => x.Id == id, CancellationToken);
    }

    private static Func<Guid, Guid, BankAccount> Named(string name) => (userId, connectionId) =>
        new BankAccount(userId, connectionId, $"ext-{name}", "TRANSACTION", name, "GBP");

    private static TransactionDetails Details(decimal amount, string description) =>
        new TransactionDetailsFaker().Generate() with
        {
            Amount = amount,
            Description = description,
            Type = amount < 0 ? TransactionType.Debit : TransactionType.Credit,
            Currency = "GBP",
            OccurredAt = Spent,
            IsPending = false,
            LabelledAsTransfer = false,
            Metadata = TransactionMetadata.None
        };

    private Guid Account(string name) => Scenario.AccountMappings[name];

    private Guid Transaction(string name) => Scenario.TransactionMappings[name];

    private Guid Tag(string name) => Scenario.ClassificationMappings[name];

    private AccountScope Scope(string username) => new(CurrentUserFor(username));

    private TransactionsService Transactions(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<TransactionsService>.Instance, TimeProvider.System,
            Scope(username));

    private ClassificationService Classifications(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<ClassificationService>.Instance, Scope(username),
            TimeProvider.System);

    private BillsService Bills(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<BillsService>.Instance,
            new BillSyncService(CurrentUserFor(username), DbContextFactory, NullLogger<BillSyncService>.Instance,
                TimeProvider.System, TestMetrics.Unobserved), TimeProvider.System, Scope(username));

    private DebtsService Debts(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<DebtsService>.Instance, TimeProvider.System,
            Scope(username));

    private SharingService Sharing(string username) =>
        new(CurrentUserFor(username), DbContextFactory, NullLogger<SharingService>.Instance,
            _services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
}

using FinanceTracker.Contracts;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure.OpenBanking;
using FinanceTracker.Infrastructure.Transactions;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Transaction = FinanceTracker.Contracts.Transactions.Transaction;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Transactions;

public class TransactionsServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    [Test]
    public async Task GetAllTransactionsAsync_NoFilters_ReturnsAllNewestFirst()
    {
        // Arrange
        DateTime now = DateTime.UtcNow;
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("old", "main", Details("Old", now.AddDays(-5))))
            .Do(s => s.TransactionScenario.WithTransaction("new", "main", Details("New", now)))
            .Do(s => s.TransactionScenario.WithTransaction("mid", "main", Details("Mid", now.AddDays(-2))));

        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> transactions = await GetAllAsync(subject, new FilterTransactions());

        // Assert
        await Assert.That(string.Join(",", transactions.Select(x => x.Description))).IsEqualTo("New,Mid,Old");
    }

    [Test]
    public async Task GetAllTransactionsAsync_MapsTransaction()
    {
        // Arrange
        DateTime occurredAt = new(2026, 4, 1, 9, 30, 0, DateTimeKind.Utc);
        TransactionDetails details = new("ext-1", "Coffee Shop", TransactionType.Debit, "PURCHASE", -4.20m, "GBP",
            occurredAt, true, ["Food"]);

        await SeedAccountAsync()
            .Do(s => s.ClassificationScenario.WithClassification("treat", Username, "Treat"))
            .Do(s => s.TransactionScenario.WithTransaction("coffee", "main", details))
            .Do(s => s.TransactionScenario.WithCustomClassification("coffee", "treat"));

        TransactionsService subject = BuildSubject();

        // Act
        Transaction transaction = (await GetAllAsync(subject, new FilterTransactions())).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(transaction.TransactionId).IsEqualTo(Scenario.TransactionMappings["coffee"]);
            await Assert.That(transaction.Description).IsEqualTo("Coffee Shop");
            await Assert.That(transaction.Amount).IsEqualTo(-4.20m);
            await Assert.That(transaction.Currency).IsEqualTo("GBP");
            await Assert.That(transaction.Pending).IsTrue();
            await Assert.That(transaction.TransactionCategory).IsEqualTo("PURCHASE");
            await Assert.That(transaction.TransactionType).IsEqualTo("Debit");
            await Assert.That(transaction.TransactionTime).IsEqualTo(occurredAt);
            await Assert.That(transaction.Tags).IsEquivalentTo(["Food", "Treat"]);
        }
    }

    [Test]
    public async Task GetAllTransactionsAsync_SearchTerm_MatchesDescriptionIgnoringCase()
    {
        // Arrange
        DateTime now = DateTime.UtcNow;
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("coffee", "main", Details("Coffee Shop", now)))
            .Do(s => s.TransactionScenario.WithTransaction("salary", "main", Details("Salary", now)));

        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> transactions = await GetAllAsync(subject, new FilterTransactions { SearchTerm = "cOFFEE" });

        // Assert
        await Assert.That(transactions.Select(x => x.Description)).IsEquivalentTo(["Coffee Shop"]);
    }

    [Test]
    public async Task GetAllTransactionsAsync_DateRange_IsInclusive()
    {
        // Arrange
        DateTime from = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = new(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc);

        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("before", "main", Details("Before", from.AddTicks(-1))))
            .Do(s => s.TransactionScenario.WithTransaction("start", "main", Details("Start", from)))
            .Do(s => s.TransactionScenario.WithTransaction("end", "main", Details("End", to)))
            .Do(s => s.TransactionScenario.WithTransaction("after", "main", Details("After", to.AddTicks(1))));

        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> transactions = await GetAllAsync(subject, new FilterTransactions { FromDate = from, ToDate = to });

        // Assert
        await Assert.That(transactions.Select(x => x.Description)).IsEquivalentTo(["Start", "End"]);
    }

    [Test]
    public async Task GetAllTransactionsAsync_AccountFilter_ReturnsOnlySelectedAccounts()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.BankAccountScenario.WithAccount("savings", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("main-tx", "main", Details("Main")))
            .Do(s => s.TransactionScenario.WithTransaction("savings-tx", "savings", Details("Savings")));

        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> transactions = await GetAllAsync(subject,
            new FilterTransactions { AccountIds = [Scenario.AccountMappings["savings"]] });

        // Assert
        await Assert.That(transactions.Select(x => x.Description)).IsEquivalentTo(["Savings"]);
    }

    [Test]
    public async Task GetAllTransactionsAsync_ProviderFilter_ReturnsOnlySelectedConnections()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.BankConnectionScenario.WithConnection("other-bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("other", "other-bank"))
            .Do(s => s.TransactionScenario.WithTransaction("main-tx", "main", Details("Main")))
            .Do(s => s.TransactionScenario.WithTransaction("other-tx", "other", Details("Other")));

        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> transactions = await GetAllAsync(subject,
            new FilterTransactions { ProviderIds = [Scenario.ConnectionMappings["other-bank"]] });

        // Assert
        await Assert.That(transactions.Select(x => x.Description)).IsEquivalentTo(["Other"]);
    }

    [Test]
    public async Task GetAllTransactionsAsync_TypeFilter_IgnoresCase()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("debit", "main",
                Details("Debit") with { Type = TransactionType.Debit, Amount = -1 }))
            .Do(s => s.TransactionScenario.WithTransaction("credit", "main",
                Details("Credit") with { Type = TransactionType.Credit, Amount = 1 }));

        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> transactions = await GetAllAsync(subject, new FilterTransactions { Types = ["CREDIT"] });

        // Assert
        await Assert.That(transactions.Select(x => x.Description)).IsEquivalentTo(["Credit"]);
    }

    [Test]
    public async Task GetAllTransactionsAsync_CategoryFilter_ReturnsOnlySelectedCategories()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("purchase", "main", Details("Purchase") with { Category = "PURCHASE" }))
            .Do(s => s.TransactionScenario.WithTransaction("atm", "main", Details("Atm") with { Category = "ATM" }))
            .Do(s => s.TransactionScenario.WithTransaction("bill", "main", Details("Bill") with { Category = "BILL_PAYMENT" }));

        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> transactions = await GetAllAsync(subject,
            new FilterTransactions { Categories = ["ATM", "BILL_PAYMENT"] });

        // Assert
        await Assert.That(transactions.Select(x => x.Description)).IsEquivalentTo(["Atm", "Bill"]);
    }

    [Test]
    public async Task GetAllTransactionsAsync_TagFilter_MatchesProviderAndCustomTags()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.ClassificationScenario.WithClassification("holiday", Username, "Holiday"))
            .Do(s => s.TransactionScenario.WithTransaction("food", "main",
                Details("Food") with { ProviderClassifications = ["Food"] }))
            .Do(s => s.TransactionScenario.WithTransaction("flight", "main",
                Details("Flight") with { ProviderClassifications = [] }))
            .Do(s => s.TransactionScenario.WithTransaction("other", "main",
                Details("Other") with { ProviderClassifications = ["Bills"] }))
            .Do(s => s.TransactionScenario.WithCustomClassification("flight", "holiday"));

        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> transactions = await GetAllAsync(subject,
            new FilterTransactions { Tags = ["Food", "Holiday"] });

        // Assert
        await Assert.That(transactions.Select(x => x.Description)).IsEquivalentTo(["Food", "Flight"]);
    }

    [Test]
    public async Task GetAllTransactionsAsync_OnlyReturnsCurrentUsersTransactions()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("other-bank", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("other", "other-bank"))
            .Do(s => s.TransactionScenario.WithTransaction("mine", "main", Details("Mine")))
            .Do(s => s.TransactionScenario.WithTransaction("theirs", "other", Details("Theirs")));

        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> transactions = await GetAllAsync(subject, new FilterTransactions());

        // Assert
        await Assert.That(transactions.Select(x => x.Description)).IsEquivalentTo(["Mine"]);
    }

    [Test]
    public async Task GetAllTransactionsAsync_WhenUserHasZeroTransactions_ReturnsEmpty()
    {
        // Arrange
        await SeedAccountAsync();
        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> transactions = await GetAllAsync(subject, new FilterTransactions());

        // Assert
        await Assert.That(transactions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetAccountsForTransactionFiltersAsync_ReturnsCurrentUsersAccounts()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.BankAccountScenario.WithAccount("savings", "bank", (userId, connectionId) =>
                new BankAccount(userId, connectionId, "acc-savings", "SAVINGS", "Rainy Day", "GBP")))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("other-bank", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("other", "other-bank"));

        TransactionsService subject = BuildSubject();

        // Act
        List<TransactionAccountFilter> accounts = await subject
            .GetAccountsForTransactionFiltersAsync(SyncTypes.All, CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(accounts.Select(x => x.AccountId)).IsEquivalentTo([
                Scenario.AccountMappings["main"], Scenario.AccountMappings["savings"]
            ]);
            await Assert.That(accounts.Single(x => x.AccountId == Scenario.AccountMappings["savings"]).AccountName)
                .IsEqualTo("Rainy Day");
        }
    }

    [Test]
    public async Task GetProvidersForTransactionFiltersAsync_ReturnsCurrentUsersConnections()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username, userId =>
                new BankConnection(userId, "ob-bank", "My Bank", "code", ["accounts"], [1], DateTime.UtcNow)))
            .Do(s => s.BankConnectionScenario.WithConnection("other-bank", OtherUsername));

        TransactionsService subject = BuildSubject();

        // Act
        List<TransactionProviderFilter> providers = await subject
            .GetProvidersForTransactionFiltersAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        TransactionProviderFilter provider = providers.Single();

        using (Assert.Multiple())
        {
            await Assert.That(provider.ProviderId).IsEqualTo(Scenario.ConnectionMappings["bank"]);
            await Assert.That(provider.ProviderName).IsEqualTo("My Bank");
        }
    }

    [Test]
    public async Task GetTypesForTransactionFiltersAsync_ReturnsDistinctTypes()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("debit-1", "main", Details("1") with { Type = TransactionType.Debit }))
            .Do(s => s.TransactionScenario.WithTransaction("debit-2", "main", Details("2") with { Type = TransactionType.Debit }))
            .Do(s => s.TransactionScenario.WithTransaction("credit", "main", Details("3") with { Type = TransactionType.Credit }));

        TransactionsService subject = BuildSubject();

        // Act
        List<TransactionTypeFilter> types = await subject.GetTypesForTransactionFiltersAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(types.Select(x => x.TransactionType)).IsEquivalentTo(["Debit", "Credit"]);
    }

    [Test]
    public async Task GetCategoriesForTransactionFiltersAsync_ReturnsDistinctCategories()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("atm-1", "main", Details("1") with { Category = "ATM" }))
            .Do(s => s.TransactionScenario.WithTransaction("atm-2", "main", Details("2") with { Category = "ATM" }))
            .Do(s => s.TransactionScenario.WithTransaction("purchase", "main", Details("3") with { Category = "PURCHASE" }));

        TransactionsService subject = BuildSubject();

        // Act
        List<TransactionCategoryFilter> categories = await subject.GetCategoriesForTransactionFiltersAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(categories.Select(x => x.TransactionCategory)).IsEquivalentTo(["ATM", "PURCHASE"]);
    }

    [Test]
    public async Task GetTagsForTransactionFiltersAsync_ReturnsDistinctProviderAndCustomTags()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.ClassificationScenario.WithClassification("holiday", Username, "Holiday"))
            .Do(s => s.ClassificationScenario.WithClassification("food", Username, "Food"))
            .Do(s => s.TransactionScenario.WithTransaction("1", "main", Details("1") with { ProviderClassifications = ["Food", "Bills"] }))
            .Do(s => s.TransactionScenario.WithTransaction("2", "main", Details("2") with { ProviderClassifications = ["Food"] }))
            .Do(s => s.TransactionScenario.WithTransaction("3", "main", Details("3") with { ProviderClassifications = [] }))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.ClassificationScenario.WithClassification("theirs", OtherUsername, "Theirs"));

        TransactionsService subject = BuildSubject();

        // Act
        List<TransactionTagFilter> tags = await subject.GetTagsForTransactionFiltersAsync(CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        await Assert.That(tags.Select(x => x.Tag)).IsEquivalentTo(["Food", "Bills", "Holiday"]);
    }

    [Test]
    public async Task GetAllTransactionsAsync_OnlyFromDate_ExcludesEarlierTransactions()
    {
        // Arrange
        DateTime from = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("before", "main", Details("Before", from.AddDays(-1))))
            .Do(s => s.TransactionScenario.WithTransaction("after", "main", Details("After", from.AddDays(1))));

        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> transactions = await GetAllAsync(subject, new FilterTransactions { FromDate = from });

        // Assert
        await Assert.That(transactions.Select(x => x.Description)).IsEquivalentTo(["After"]);
    }

    [Test]
    public async Task GetAllTransactionsAsync_OnlyToDate_ExcludesLaterTransactions()
    {
        // Arrange
        DateTime to = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("before", "main", Details("Before", to.AddDays(-1))))
            .Do(s => s.TransactionScenario.WithTransaction("after", "main", Details("After", to.AddDays(1))));

        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> transactions = await GetAllAsync(subject, new FilterTransactions { ToDate = to });

        // Assert
        await Assert.That(transactions.Select(x => x.Description)).IsEquivalentTo(["Before"]);
    }

    [Test]
    public async Task GetAllTransactionsAsync_UnknownTypeFilter_MatchesNothing()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("debit", "main", Details("Debit")));

        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> transactions = await GetAllAsync(subject, new FilterTransactions { Types = ["Refund"] });

        // Assert
        await Assert.That(transactions.Count).IsEqualTo(0);
    }

    private Task<TestingScenario> SeedAccountAsync() =>
        Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"));

    private static TransactionDetails Details(string description, DateTime? occurredAt = null) =>
        new TransactionDetailsFaker().Generate() with
        {
            Description = description, OccurredAt = occurredAt ?? DateTime.UtcNow
        };

    private Task<List<Transaction>> GetAllAsync(TransactionsService subject, FilterTransactions filter) =>
        subject.GetAllTransactionsAsync(filter, SyncTypes.All, CancellationToken).ToListAsync(CancellationToken).AsTask();

    private TransactionsService BuildSubject() => new(CurrentUserFor(Username), DbContextFactory,
        Substitute.For<IOpenBankingService>(), NullLogger<TransactionsService>.Instance);
}

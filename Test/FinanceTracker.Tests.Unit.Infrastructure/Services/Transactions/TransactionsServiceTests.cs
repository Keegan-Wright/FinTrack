using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure.Transactions;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Transaction = FinanceTracker.Contracts.Transactions.Transaction;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.Household;
using FinanceTracker.Infrastructure.OpenBanking.Transfers;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Domain.SavingsGoals;
using FinanceTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;

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
            occurredAt, true, ["Food"], TransactionMetadata.None, false);

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
    public async Task GetAllTransactionsAsync_IncludesTransfersAndSaysWhetherEachCounts()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("shopping", "main"))
            .Do(s => s.TransactionScenario.WithTransaction("to-savings", "main"))
            .Do(s => s.TransactionScenario.WithTransfer("to-savings"))
            .Do(s => s.TransactionScenario.WithTransaction("to-friend", "main"))
            .Do(s => s.TransactionScenario.WithTransfer("to-friend", TransferDecision.NeedsReview));

        TransactionsService subject = BuildSubject();

        // Act
        Dictionary<Guid, bool> counts = (await GetAllAsync(subject, new FilterTransactions()))
            .ToDictionary(static x => x.TransactionId, static x => x.CountsAsIncomeOrSpending);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(counts[Scenario.TransactionMappings["shopping"]]).IsTrue();
            await Assert.That(counts[Scenario.TransactionMappings["to-savings"]]).IsFalse();
            await Assert.That(counts[Scenario.TransactionMappings["to-friend"]]).IsTrue();
        }
    }

    [Test]
    public async Task GetAllTransactionsAsync_OnlyAFromDate_ReturnsEverythingSinceIncludingToday()
    {
        // Arrange
        DateTime now = DateTime.UtcNow;
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("long-ago", "main", Details("Long ago", now.AddDays(-400))))
            .Do(s => s.TransactionScenario.WithTransaction("before", "main", Details("Before", now.AddDays(-11))))
            .Do(s => s.TransactionScenario.WithTransaction("after", "main", Details("After", now.AddDays(-9))))
            .Do(s => s.TransactionScenario.WithTransaction("today", "main", Details("Today", now)));

        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> transactions =
            await GetAllAsync(subject, new FilterTransactions { FromDate = now.AddDays(-10) });

        // Assert
        await Assert.That(transactions.Select(static x => x.Description)).IsEquivalentTo(["After", "Today"]);
    }

    [Test]
    public async Task GetAllTransactionsAsync_OnlyAToDate_ReturnsEverythingBeforeIt()
    {
        // Arrange
        DateTime now = DateTime.UtcNow;
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("long-ago", "main", Details("Long ago", now.AddDays(-2000))))
            .Do(s => s.TransactionScenario.WithTransaction("before", "main", Details("Before", now.AddDays(-11))))
            .Do(s => s.TransactionScenario.WithTransaction("after", "main", Details("After", now.AddDays(-9))))
            .Do(s => s.TransactionScenario.WithTransaction("today", "main", Details("Today", now)));

        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> transactions =
            await GetAllAsync(subject, new FilterTransactions { ToDate = now.AddDays(-10) });

        // Assert
        await Assert.That(transactions.Select(static x => x.Description)).IsEquivalentTo(["Long ago", "Before"]);
    }

    [Test]
    public async Task GetAllTransactionsAsync_NamesTheAccountAndWhoATransferWasWith()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank", static (userId, connectionId) =>
                new BankAccount(userId, connectionId, "acc-main", "TRANSACTION", "Joint", "GBP")))
            .Do(s => s.HouseholdMemberScenario.WithHouseholdMember("partner", Username, static userId =>
                new HouseholdMember(userId, "Jane", "Smith", null)))
            .Do(s => s.HouseholdMemberScenario.WithPayeeName("partner", "J Smith"))
            .Do(s => s.TransactionScenario.WithTransaction("rent", "main", Details("J SMITH 01JAN26")));

        await new TransferMatchingService(CurrentUserFor(Username), DbContextFactory,
            NullLogger<TransferMatchingService>.Instance, TimeProvider.System).MatchAsync(CancellationToken);

        TransactionsService subject = BuildSubject();

        // Act
        Transaction transaction = (await GetAllAsync(subject, new FilterTransactions())).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(transaction.AccountId).IsEqualTo(Scenario.AccountMappings["main"]);
            await Assert.That(transaction.AccountName).IsEqualTo("Joint");
            await Assert.That(transaction.Transfer!.State).IsEqualTo(TransferState.Transfer);
            await Assert.That(transaction.Transfer.Reason).IsEqualTo(TransferMatchReason.HouseholdMember);
            await Assert.That(transaction.Transfer.PartnerHouseholdMemberName).IsEqualTo("Jane Smith");
        }
    }

    [Test]
    public async Task TransferMatchReason_HasTheSameNamesAndValuesAsTheDomains()
    {
        // Act
        List<string> mapped = Enum.GetValues<Domain.OpenBanking.Transfers.TransferReason>()
            .Select(static x => ((TransferMatchReason)(int)x).ToString())
            .ToList();

        // Assert
        await Assert.That(mapped)
            .IsEquivalentTo(Enum.GetNames<Domain.OpenBanking.Transfers.TransferReason>());
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
    public async Task GetAllTransactionsAsync_SaysWhatEachPaysAndCountsTowards()
    {
        // Arrange
        await SeedLinksAsync();
        TransactionsService subject = BuildSubject();

        // Act
        Dictionary<string, Transaction> transactions = (await GetAllAsync(subject, new FilterTransactions()))
            .ToDictionary(static x => x.Description);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(transactions["Streaming"].Bill!.BillName).IsEqualTo("Streaming");
            await Assert.That(transactions["Streaming"].Bill!.IsSubscription).IsTrue();
            await Assert.That(transactions["Rent"].Bill!.IsSubscription).IsFalse();
            await Assert.That(transactions["Car payment"].Debt!.DebtName).IsEqualTo("Car loan");
            await Assert.That(transactions["Saved"].SavingsGoals.Select(static x => x.Name))
                .IsEquivalentTo(["Holiday", "Rainy day"]);
            await Assert.That(transactions["Saved"].CustomTags).IsEquivalentTo(["Holiday"]);
            await Assert.That(transactions["Rent"].SavingsGoals).IsEmpty();
        }
    }

    [Test]
    public async Task GetAllTransactionsAsync_BillFilter_ReturnsOnlyItsPayments()
    {
        // Arrange
        await SeedLinksAsync();
        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> transactions = await GetAllAsync(subject,
            new FilterTransactions { BillIds = [Scenario.BillMappings["streaming"]] });

        // Assert
        await Assert.That(transactions.Select(static x => x.Description)).IsEquivalentTo(["Streaming"]);
    }

    [Test]
    public async Task GetAllTransactionsAsync_DebtFilter_ReturnsOnlyItsPayments()
    {
        // Arrange
        await SeedLinksAsync();
        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> transactions = await GetAllAsync(subject,
            new FilterTransactions { DebtIds = [Scenario.DebtMappings["loan"]] });

        // Assert
        await Assert.That(transactions.Select(static x => x.Description)).IsEquivalentTo(["Car payment"]);
    }

    [Test]
    public async Task GetAllTransactionsAsync_SavingsGoalFilter_ReturnsWhatCountsTowardsIt()
    {
        // Arrange
        await SeedLinksAsync();
        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> byTag = await GetAllAsync(subject,
            new FilterTransactions { SavingsGoalIds = [Scenario.SavingsGoalMappings["holiday"]] });
        List<Transaction> byAccount = await GetAllAsync(subject,
            new FilterTransactions { SavingsGoalIds = [Scenario.SavingsGoalMappings["rainy-day"]] });

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(byTag.Select(static x => x.Description)).IsEquivalentTo(["Saved"]);
            await Assert.That(byAccount.Select(static x => x.Description)).IsEquivalentTo(["Saved", "Interest"]);
        }
    }

    [Test]
    public async Task GetAllTransactionsAsync_AnotherUsersSavingsGoal_MatchesNothing()
    {
        // Arrange
        await SeedLinksAsync()
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("theirs", OtherUsername, userId =>
                new SavingsGoal(userId, "Theirs", 0, 0, 0, null)));
        TransactionsService subject = BuildSubject();

        // Act
        List<Transaction> transactions = await GetAllAsync(subject,
            new FilterTransactions { SavingsGoalIds = [Scenario.SavingsGoalMappings["theirs"]] });

        // Assert
        await Assert.That(transactions).IsEmpty();
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
            .GetAccountsForTransactionFiltersAsync(CancellationToken).ToListAsync(CancellationToken);

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
                new BankConnection(userId, "ob-bank", "My Bank", "code", ["accounts"], [1], DateTime.UtcNow, DateTime.UtcNow.AddDays(90))))
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

    [Test]
    public async Task GetAllTransactionsAsync_RuleDecisions_NameTheRule()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.ClassificationScenario.WithClassification("shopping", Username, "Shopping"))
            .Do(s => s.RuleScenario.WithRule("groceries", Username, userId => new Rule(userId, "Groceries", 0,
                RuleMatchMode.All, [new RuleCondition(RuleField.Description, RuleOperator.Contains, "GROCER")],
                [
                    new RuleAction(RuleActionType.AddTag, Scenario.ClassificationMappings["shopping"]),
                    RuleAction.MarkNotATransfer()
                ])))
            .Do(s => s.TransactionScenario.WithTransaction("bought", "main", Details("GROCER STORE")));
        await ApplyRulesAsync();

        TransactionsService subject = BuildSubject();

        // Act
        Transaction transaction = (await GetAllAsync(subject, new FilterTransactions())).Single();

        // Assert
        TransactionRuleTag ruleTag = transaction.TagsFromRules.Single();
        using (Assert.Multiple())
        {
            await Assert.That(ruleTag.Tag).IsEqualTo("Shopping");
            await Assert.That(ruleTag.RuleName).IsEqualTo("Groceries");
            await Assert.That(transaction.Transfer!.DecidedBy).IsEqualTo(TransferDecidedBy.Rule);
            await Assert.That(transaction.Transfer.RuleName).IsEqualTo("Groceries");
        }
    }

    [Test]
    public async Task HideBankClassificationAsync_LeavesItOutOfTagsAndFilters()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("mortgage", "main", Details("MORTGAGE") with
            {
                ProviderClassifications = ["Bills", "TV"]
            }));
        TransactionsService subject = BuildSubject();

        // Act
        Transaction hidden = await subject.HideBankClassificationAsync(Scenario.TransactionMappings["mortgage"],
            new ChangeBankClassification { Classification = "tv" }, CancellationToken);
        List<string> filterTags = await subject.GetTagsForTransactionFiltersAsync(CancellationToken)
            .Select(static x => x.Tag).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(hidden.Tags).IsEquivalentTo(["Bills"]);
            await Assert.That(hidden.BankClassifications).IsEquivalentTo(["Bills"]);
            await Assert.That(hidden.HiddenBankClassifications.Single().Classification).IsEqualTo("TV");
            await Assert.That(hidden.HiddenBankClassifications.Single().RuleName).IsNull();
            await Assert.That(filterTags).DoesNotContain("TV");
        }
    }

    [Test]
    public async Task ShowBankClassificationAsync_HiddenByTheUser_ShowsItAgain()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("mortgage", "main", Details("MORTGAGE") with
            {
                ProviderClassifications = ["Bills", "TV"]
            }));
        TransactionsService subject = BuildSubject();
        Guid id = Scenario.TransactionMappings["mortgage"];
        await subject.HideBankClassificationAsync(id, new ChangeBankClassification { Classification = "TV" },
            CancellationToken);

        // Act
        Transaction shown = await subject.ShowBankClassificationAsync(id,
            new ChangeBankClassification { Classification = "TV" }, CancellationToken);

        // Assert
        await Assert.That(shown.Tags).IsEquivalentTo(["Bills", "TV"]);
    }

    [Test]
    public async Task HideBankClassificationAsync_TheBankDidntSendIt_Throws()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("mortgage", "main", Details("MORTGAGE") with
            {
                ProviderClassifications = ["Bills"]
            }));

        // Act & Assert
        await Assert.That(async () => await BuildSubject().HideBankClassificationAsync(
                Scenario.TransactionMappings["mortgage"], new ChangeBankClassification { Classification = "TV" },
                CancellationToken))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task HideBankClassificationAsync_AnotherUsersTransaction_Throws()
    {
        // Arrange
        await SeedAccountAsync();
        await Scenario.Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("theirs", "theirs"))
            .Do(s => s.TransactionScenario.WithTransaction("theirs", "theirs", Details("MORTGAGE") with
            {
                ProviderClassifications = ["TV"]
            }));

        // Act & Assert
        await Assert.That(async () => await BuildSubject().HideBankClassificationAsync(
                Scenario.TransactionMappings["theirs"], new ChangeBankClassification { Classification = "TV" },
                CancellationToken))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task TransactionsFromBeforeHiding_LoadAndFilterAsNothingHidden()
    {
        // Arrange, as the migration left existing rows before it filled them in
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("old", "main", Details("OLD") with
            {
                ProviderClassifications = ["Bills"]
            }));
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE \"BankTransactions\" SET \"HiddenClassifications\" = NULL", CancellationToken);
        }

        TransactionsService subject = BuildSubject();

        // Act
        List<string> filterTags = await subject.GetTagsForTransactionFiltersAsync(CancellationToken)
            .Select(static x => x.Tag).ToListAsync(CancellationToken);
        Transaction transaction = (await GetAllAsync(subject, new FilterTransactions())).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(filterTags).IsEquivalentTo(["Bills"]);
            await Assert.That(transaction.Tags).IsEquivalentTo(["Bills"]);
            await Assert.That(transaction.HiddenBankClassifications).IsEmpty();
        }
    }

    [Test]
    public async Task SplitAsync_SavesTheParts_WithTheirTags()
    {
        // Arrange
        await SeedSupermarketAsync();
        TransactionsService subject = BuildSubject();
        Guid groceries = Scenario.ClassificationMappings["groceries"];
        Guid household = Scenario.ClassificationMappings["household"];

        // Act
        await subject.SplitAsync(Scenario.TransactionMappings["shop"], new SplitTransaction
        {
            Parts =
            [
                new SplitTransactionPart { Amount = -60m, TagId = groceries, Note = "  Weekly shop " },
                new SplitTransactionPart { Amount = -30m, TagId = household, Note = " " },
                new SplitTransactionPart { Amount = -10m }
            ]
        }, CancellationToken);
        Transaction saved = (await GetAllAsync(subject, new FilterTransactions())).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(saved.Splits.Select(static x => (x.Amount, x.Tag, x.Note))).IsEquivalentTo(
            [
                (-60m, (string?)"Groceries", (string?)"Weekly shop"),
                (-30m, "Household", null),
                (-10m, null, null)
            ]);
            await Assert.That(saved.CustomTags).IsEquivalentTo(["Groceries", "Household"]);
        }
    }

    [Test]
    public async Task SplitAsync_PartsThatDontAddUp_ThrowsCannotSplit()
    {
        // Arrange
        await SeedSupermarketAsync();

        // Act & Assert
        await Assert.That(async () => await BuildSubject().SplitAsync(Scenario.TransactionMappings["shop"],
                Parts(-60m, -30m), CancellationToken))
            .Throws<CannotSplitTransactionException>();
    }

    [Test]
    public async Task SplitAsync_Pending_ThrowsCannotSplit()
    {
        // Arrange
        await SeedAccountAsync()
            .Do(s => s.TransactionScenario.WithTransaction("shop", "main", Details("Supermarket") with
            {
                Amount = -100m, IsPending = true
            }));

        // Act & Assert
        await Assert.That(async () => await BuildSubject().SplitAsync(Scenario.TransactionMappings["shop"],
                Parts(-60m, -40m), CancellationToken))
            .Throws<CannotSplitTransactionException>();
    }

    [Test]
    public async Task SplitAsync_AnotherUsersTag_ThrowsCannotSplit()
    {
        // Arrange
        await SeedSupermarketAsync();
        await Scenario.Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.ClassificationScenario.WithClassification("theirs", OtherUsername, "Theirs"));
        SplitTransaction request = Parts(-60m, -40m);
        request.Parts[0].TagId = Scenario.ClassificationMappings["theirs"];

        // Act & Assert
        await Assert.That(async () => await BuildSubject().SplitAsync(Scenario.TransactionMappings["shop"],
                request, CancellationToken))
            .Throws<CannotSplitTransactionException>();
    }

    [Test]
    public async Task SplitAsync_AnotherUsersTransaction_ThrowsInvalidOperation()
    {
        // Arrange
        await SeedAccountAsync();
        await Scenario.Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("theirs", OtherUsername))
            .Do(s => s.BankAccountScenario.WithAccount("theirs", "theirs"))
            .Do(s => s.TransactionScenario.WithTransaction("theirs", "theirs", Details("Supermarket") with
            {
                Amount = -100m, IsPending = false
            }));

        // Act & Assert
        await Assert.That(async () => await BuildSubject().SplitAsync(Scenario.TransactionMappings["theirs"],
                Parts(-60m, -40m), CancellationToken))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task UnsplitAsync_KeepsThePartsTagsOnTheWhole()
    {
        // Arrange
        await SeedSupermarketAsync();
        TransactionsService subject = BuildSubject();
        Guid id = Scenario.TransactionMappings["shop"];
        SplitTransaction request = Parts(-60m, -40m);
        request.Parts[0].TagId = Scenario.ClassificationMappings["groceries"];
        await subject.SplitAsync(id, request, CancellationToken);

        // Act
        Transaction whole = await subject.UnsplitAsync(id, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(whole.Splits).IsEmpty();
            await Assert.That(whole.CustomTags).IsEquivalentTo(["Groceries"]);
        }
    }

    private Task<TestingScenario> SeedSupermarketAsync() =>
        SeedAccountAsync()
            .Do(s => s.ClassificationScenario.WithClassification("groceries", Username, "Groceries"))
            .Do(s => s.ClassificationScenario.WithClassification("household", Username, "Household"))
            .Do(s => s.TransactionScenario.WithTransaction("shop", "main", Details("Supermarket") with
            {
                Amount = -100m, IsPending = false
            }));

    private static SplitTransaction Parts(params decimal[] amounts) => new()
    {
        Parts = amounts.Select(static x => new SplitTransactionPart { Amount = x }).ToList()
    };

    private async Task ApplyRulesAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        List<Rule> rules = await context.Rules.ToListAsync(CancellationToken);
        foreach (BankTransaction transaction in await context.BankTransactions.ToListAsync(CancellationToken))
        {
            RuleEvaluator.Apply(rules, transaction, DateTime.UtcNow);
        }

        await context.SaveChangesAsync(CancellationToken);
    }

    /// <summary>
    /// Rent paid by standing order, a streaming subscription, a car loan payment, and money saved into a savings
    /// account tagged for a holiday goal, alongside the account's interest.
    /// </summary>
    private async Task<TestingScenario> SeedLinksAsync()
    {
        DateTime day = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
        await SeedAccountAsync()
            .Do(s => s.BankAccountScenario.WithAccount("savings", "bank"))
            .Do(s => s.ClassificationScenario.WithClassification("holiday-tag", Username, "Holiday"))
            .Do(s => s.TransactionScenario.WithTransaction("rent", "main", Details("Rent", day)))
            .Do(s => s.TransactionScenario.WithTransaction("streaming", "main", Details("Streaming", day)))
            .Do(s => s.TransactionScenario.WithTransaction("car", "main", Details("Car payment", day)))
            .Do(s => s.TransactionScenario.WithTransaction("saved", "savings", Details("Saved", day)))
            .Do(s => s.TransactionScenario.WithTransaction("interest", "savings", Details("Interest", day)))
            .Do(s => s.TransactionScenario.WithCustomClassification("saved", "holiday-tag"))
            .Do(s => s.BillScenario.WithBill("rent", Username, "main", (userId, accountId) =>
                new Bill(userId, accountId, "Rent", "Landlord", BillFrequency.Monthly, 650m,
                    new DateOnly(2026, 1, 1), BillSource.StandingOrder, Guid.NewGuid())))
            .Do(s => s.BillScenario.WithBill("streaming", Username, "main", (userId, accountId) =>
                new Bill(userId, accountId, "Streaming", "Streamco", BillFrequency.Monthly, 9.99m,
                    new DateOnly(2026, 1, 1), BillSource.Detected)))
            .Do(s => s.DebtScenario.WithDebt("loan", Username, userId =>
                new Debt(userId, "Car loan", null, 1000m, new DateOnly(2026, 1, 1), 0m, 100m)))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("holiday", Username, userId =>
            {
                SavingsGoal goal = new(userId, "Holiday", 0, 0, 1000, null);
                goal.TrackTags([Scenario.ClassificationMappings["holiday-tag"]], 0m, new DateOnly(2026, 1, 1));
                return goal;
            }))
            .Do(s => s.SavingsGoalScenario.WithSavingsGoal("rainy-day", Username, userId =>
            {
                SavingsGoal goal = new(userId, "Rainy day", 0, 0, 1000, null);
                goal.TrackAccount(Scenario.AccountMappings["savings"]);
                return goal;
            }));

        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        await LinkAsync(context, "rent", x => x.DecideBillPayment(BillPayment.Matched(Scenario.BillMappings["rent"],
            day)));
        await LinkAsync(context, "streaming", x => x.DecideBillPayment(
            BillPayment.Matched(Scenario.BillMappings["streaming"], day)));
        Debt loan = await context.Debts.SingleAsync(CancellationToken);
        await LinkAsync(context, "car", x => x.PayDebt(loan, day));
        await context.SaveChangesAsync(CancellationToken);
        return Scenario;
    }

    private async Task LinkAsync(FinanceTrackerContext context, string transactionName, Action<BankTransaction> link)
    {
        Guid id = Scenario.TransactionMappings[transactionName];
        link(await context.BankTransactions.SingleAsync(x => x.Id == id, CancellationToken));
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
        subject.GetAllTransactionsAsync(filter, CancellationToken).ToListAsync(CancellationToken).AsTask();

    private TransactionsService BuildSubject() => new(CurrentUserFor(Username), DbContextFactory,
        NullLogger<TransactionsService>.Instance, TimeProvider.System);
}

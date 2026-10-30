using FinanceTracker.Contracts.Account;
using FinanceTracker.Contracts.Calendar;
using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.Reports.Account;
using FinanceTracker.Contracts.Reports.Category;
using FinanceTracker.Contracts.Reports.SpentInTimePeriod;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;

namespace FinanceTracker.Tests.Integration.Api;

/// <summary>
/// The read endpoints over a user's synced data accounts, transactions, reports, dashboard and calendar. Each test
/// seeds one account with a debit and a credit in March 2026.
/// </summary>
public class TransactionDataApiTests : IntegrationTestBase
{
    [Test]
    public async Task HideThenShowBankClassification_ChangesTheTagsAndWhatsHidden()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("mortgage", "main",
                new TransactionDetailsFaker().Generate() with { ProviderClassifications = ["Bills", "TV"] }));

        ITransactionsService service = ApiFor(user).Get<ITransactionsService>();
        Guid id = Scenario.TransactionMappings["mortgage"];

        // Act
        Transaction hidden = await service.HideBankClassificationAsync(id,
            new ChangeBankClassification { Classification = "TV" }, CancellationToken);
        Transaction shown = await service.ShowBankClassificationAsync(id,
            new ChangeBankClassification { Classification = "TV" }, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(hidden.Tags).IsEquivalentTo(["Bills"]);
            await Assert.That(hidden.HiddenBankClassifications.Single().Classification).IsEqualTo("TV");
            await Assert.That(shown.Tags).IsEquivalentTo(["Bills", "TV"]);
        }
    }

    [Test]
    public async Task HideBankClassification_TheBankDidntSend_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("mortgage", "main",
                new TransactionDetailsFaker().Generate() with { ProviderClassifications = ["Bills"] }));

        ITransactionsService service = ApiFor(user).Get<ITransactionsService>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HideBankClassificationAsync(
            Scenario.TransactionMappings["mortgage"], new ChangeBankClassification { Classification = "TV" },
            CancellationToken));
    }

    private static readonly DateTime Coffee = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Salary = new(2026, 3, 25, 9, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Accounts_AreListedWithTheirRecentTransactions()
    {
        // Arrange
        SignedInUser user = await SeedUserAsync();

        // Act
        List<AccountAndTransactions> accounts = await ApiFor(user).Get<IAccountService>()
            .GetAccountsAndMostRecentTransactionsAsync(5, CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(accounts).Count().IsEqualTo(1);
            await Assert.That(accounts[0].Transactions!.Select(x => x.Amount)).IsEquivalentTo([-3.50m, 2000m]);
        }
    }

    [Test]
    public async Task Transactions_AreFilteredByTheSharedFilterBody()
    {
        // Arrange
        SignedInUser user = await SeedUserAsync();
        ITransactionsService service = ApiFor(user).Get<ITransactionsService>();

        // Act
        List<Transaction> debits = await service
            .GetAllTransactionsAsync(new FilterTransactions { Types = ["debit"] }, CancellationToken)
            .ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(debits).Count().IsEqualTo(1);
            await Assert.That(debits[0].Amount).IsEqualTo(-3.50m);
            await Assert.That(debits[0].TransactionTime).IsEqualTo(Coffee);
            await Assert.That(debits[0].Tags).Contains("Groceries");
        }
    }

    [Test]
    public async Task TransactionFilters_ListTheUsersValues()
    {
        // Arrange
        SignedInUser user = await SeedUserAsync();
        ITransactionsService service = ApiFor(user).Get<ITransactionsService>();

        // Act
        List<TransactionAccountFilter> accounts = await service.GetAccountsForTransactionFiltersAsync(CancellationToken).ToListAsync(CancellationToken);
        List<TransactionProviderFilter> providers = await service.GetProvidersForTransactionFiltersAsync(CancellationToken).ToListAsync(CancellationToken);
        List<TransactionTypeFilter> types = await service.GetTypesForTransactionFiltersAsync(CancellationToken).ToListAsync(CancellationToken);
        List<TransactionCategoryFilter> categories = await service.GetCategoriesForTransactionFiltersAsync(CancellationToken).ToListAsync(CancellationToken);
        List<TransactionTagFilter> tags = await service.GetTagsForTransactionFiltersAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(accounts.Select(x => x.AccountId)).IsEquivalentTo([Scenario.AccountMappings["main"]]);
            await Assert.That(providers.Select(x => x.ProviderId)).IsEquivalentTo([Scenario.ConnectionMappings["bank"]]);
            await Assert.That(types.Select(x => x.TransactionType)).IsEquivalentTo(["Debit", "Credit"]);
            await Assert.That(categories.Select(x => x.TransactionCategory)).IsEquivalentTo(["PURCHASE", "CREDIT"]);
            await Assert.That(tags.Select(x => x.Tag)).Contains("Groceries");
        }
    }

    [Test]
    public async Task Reports_ComeBackWithTheirBreakdowns()
    {
        // Arrange
        SignedInUser user = await SeedUserAsync();
        IReportService service = ApiFor(user).Get<IReportService>();
        FilterTransactions request = new();

        // Act
        List<SpentInTimePeriodReport> inPeriod = await service.GetSpentInTimePeriodReportAsync(request, CancellationToken).ToListAsync(CancellationToken);
        List<SpentInCategoryReport> byCategory = await service.GetCategoryBreakdownReportAsync(request, CancellationToken).ToListAsync(CancellationToken);
        List<SpentInAccountReport> byAccount = await service.GetAccountBreakdownReportAsync(request, CancellationToken).ToListAsync(CancellationToken);

        // Assert
        SpentInTimePeriodReport report = inPeriod.Single();
        using (Assert.Multiple())
        {
            await Assert.That(report.TotalIn).IsEqualTo(2000m);
            await Assert.That(report.TotalOut).IsEqualTo(-3.50m);
            // The breakdowns only arrive if the getter only collections deserialise
            await Assert.That(report.YearlyBreakdown.Single().Year).IsEqualTo(2026);
            await Assert.That(report.YearlyBreakdown.Single().MonthlyBreakdown.Single().DailyBreakdown.Select(x => x.Day))
                .IsEquivalentTo([10, 25]);
            await Assert.That(byCategory.Select(x => x.Category)).Contains("Groceries");
            await Assert.That(byAccount.Single().TotalTransactions).IsEqualTo(2);
        }
    }

    [Test]
    public async Task Dashboard_TotalsThePeriodAndListsUpcomingPayments()
    {
        // Arrange
        SignedInUser user = await SeedUserAsync();
        IDashboardService service = ApiFor(user).Get<IDashboardService>();

        // Act
        SpentInTimePeriod spent = await service.GetSpentInTimePeriod(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc), CancellationToken);
        List<UpcomingPayment> upcoming = await service.GetUpcomingPaymentsAsync(5, CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(spent.TotalIn).IsEqualTo(2000m);
            await Assert.That(spent.TotalOut).IsEqualTo(-3.50m);
            await Assert.That(upcoming.Select(x => x.PaymentName)).IsEquivalentTo(["Landlord"]);
        }
    }

    [Test]
    public async Task Calendar_PlacesTransactionsOnTheirDays()
    {
        // Arrange
        SignedInUser user = await SeedUserAsync();

        // Act
        List<CalendarItems> days = await ApiFor(user).Get<ICalendarService>()
            .GetMonthItemsAsync(3, 2026, CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(days).Count().IsEqualTo(31);
            await Assert.That(days.Single(x => x.Date.Day == 10).Transactions.Select(x => x.Amount)).IsEquivalentTo([-3.50m]);
            await Assert.That(days.Single(x => x.Date.Day == 25).Transactions.Select(x => x.Amount)).IsEquivalentTo([2000m]);
        }
    }

    private async Task<SignedInUser> SeedUserAsync()
    {
        SignedInUser user = await RegisterUserAsync();

        StandingOrderDetails rent = new StandingOrderDetailsFaker().Generate() with
        {
            Payee = "Landlord",
            NextPaymentDate = DateTime.UtcNow.AddDays(10)
        };

        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.BankAccountScenario.WithBalance("main"))
            .Do(s => s.BankAccountScenario.WithStandingOrders("main", rent))
            .Do(s => s.TransactionScenario.WithTransaction("coffee", "main", Details(-3.50m, TransactionType.Debit,
                "PURCHASE", Coffee, ["Groceries"])))
            .Do(s => s.TransactionScenario.WithTransaction("salary", "main", Details(2000m, TransactionType.Credit,
                "CREDIT", Salary, [])));

        return user;
    }

    private static TransactionDetails Details(decimal amount, TransactionType type, string category,
        DateTime occurredAt, string[] classifications) =>
        new TransactionDetailsFaker().Generate() with
        {
            Amount = amount,
            Type = type,
            Category = category,
            OccurredAt = occurredAt,
            ProviderClassifications = classifications
        };
}

using AngleSharp.Dom;
using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using DebtDetailsPage = FinanceTracker.Client.Pages.DebtDetails;

namespace FinanceTracker.Tests.Unit.Client.Debts;

public class DebtDetailsPageTests : ComponentTestBase
{
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    private readonly IDebtsService _debtsService = Substitute.For<IDebtsService>();
    private readonly ApplicationState _applicationState = new();
    private readonly Debt _debt = DebtTextTests.Debt(lender: "Car Finance Co", rate: 6.9m,
        forecast: DebtTextTests.Paying(4, new DateOnly(2027, 3, 2), interest: 20m));

    public DebtDetailsPageTests()
    {
        AddBunitPersistentComponentState();
        ITransactionsService transactionsService = Substitute.For<ITransactionsService>();
        transactionsService.GetAccountsForTransactionFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<TransactionAccountFilter>());

        Services.AddSingleton(_debtsService);
        Services.AddSingleton(transactionsService);
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
    }

    [Test]
    public async Task Render_Debt_ShowsWhatsOwedItsTermsAndPayments()
    {
        // Arrange
        using CultureScope culture = new();
        Detail(new DebtPaymentItem
        {
            TransactionId = Guid.NewGuid(), Description = "CAR FINANCE", AccountName = "Current", Amount = 100m,
            PaidAt = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc), DecidedBy = TransferDecidedBy.Rule,
            RuleName = "Car finance"
        });

        // Act
        IRenderedComponent<DebtDetailsPage> cut = RenderPage();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("Car Finance Co · 6.9% APR · £250.00 a month");
            await Assert.That(cut.Markup).Contains("£900.00 left of £1,000.00");
            await Assert.That(cut.Markup).Contains("Paid off by Mar 2027, 4 more payments");
            await Assert.That(cut.Markup).Contains("£20.00 interest still to pay");
            await Assert.That(cut.Markup).Contains("CAR FINANCE");
            await Assert.That(cut.Markup).Contains("12 Oct 2026 · Current · By your rule Car finance");
        }
    }

    [Test]
    public async Task Render_NoPayments_SaysHowToLinkThem()
    {
        // Arrange
        Detail();

        // Act
        IRenderedComponent<DebtDetailsPage> cut = RenderPage();

        // Assert
        await Assert.That(cut.FindAll("a[href='Rules/new']")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Render_NotTheirs_SaysItsGone()
    {
        // Arrange
        _debtsService.GetDebtAsync(_debt.Id, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException());

        // Act
        IRenderedComponent<DebtDetailsPage> cut = RenderPage();

        // Assert
        await Assert.That(cut.Markup).Contains("That debt doesn't exist any more");
    }

    [Test]
    public async Task WorkItOut_PayingMore_ComparesWithTheForecast()
    {
        // Arrange
        using CultureScope culture = new();
        Detail();
        _debtsService.ForecastAsync(_debt.Id, 300m, Arg.Any<CancellationToken>())
            .Returns(DebtTextTests.Paying(3, new DateOnly(2027, 2, 2), interest: 12m));
        IRenderedComponent<DebtDetailsPage> cut = RenderPage();

        // Act
        await cut.FindAll("button").Single(x => x.TextContent.Trim() == "Work it out").ClickAsync(new());

        // Assert
        await Assert.That(cut.Markup)
            .Contains("At £300.00 a month it's paid off by Feb 2027, 1 month sooner, saving £8.00 interest");
    }

    private void Detail(params DebtPaymentItem[] payments) =>
        _debtsService.GetDebtAsync(_debt.Id, Arg.Any<CancellationToken>()).Returns(new DebtDetail
        {
            Debt = _debt,
            Payments = payments,
            History =
            [
                new DebtBalancePoint { On = new DateOnly(2026, 1, 1), Owed = 1000m },
                new DebtBalancePoint { On = new DateOnly(2026, 11, 2), Owed = 900m }
            ]
        });

    private IRenderedComponent<DebtDetailsPage> RenderPage() =>
        Render<DebtDetailsPage>(p => p
            .Add(x => x.Id, _debt.Id)
            .AddCascadingValue(_applicationState));
}

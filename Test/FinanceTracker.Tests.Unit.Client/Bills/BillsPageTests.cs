using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using BillsPage = FinanceTracker.Client.Pages.Bills;

namespace FinanceTracker.Tests.Unit.Client.Bills;

public class BillsPageTests : ComponentTestBase
{
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    private readonly IBillsService _billsService = Substitute.For<IBillsService>();
    private readonly ApplicationState _applicationState = new();

    public BillsPageTests()
    {
        AddBunitPersistentComponentState();
        ITransactionsService transactionsService = Substitute.For<ITransactionsService>();
        transactionsService.GetAccountsForTransactionFiltersAsync(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<TransactionAccountFilter>());

        Services.AddSingleton(_billsService);
        Services.AddSingleton(transactionsService);
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));

        _billsService.GetBillsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { BillTextTests.Bill() }.ToAsyncEnumerable());
        _billsService.GetMonthAsync(2026, 10, Arg.Any<CancellationToken>()).Returns(Overview(10));
        _billsService.GetMonthAsync(2026, 9, Arg.Any<CancellationToken>()).Returns(Overview(9));
    }

    [Test]
    public async Task Render_ThisMonth_ShowsTheTotalsAndEachBill()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        IRenderedComponent<BillsPage> cut = RenderPage();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("October 2026");
            await Assert.That(cut.Markup).Contains("Committed a month");
            await Assert.That(cut.Markup).Contains("£9.99 of £9.99 paid");
            await Assert.That(cut.Markup).Contains("Energy");
            await Assert.That(cut.Markup).Contains("Paid 12 Oct");
            await Assert.That(cut.FindAll("[aria-label='Actions for Energy']")).Count().IsEqualTo(1);
        }
    }

    [Test]
    public async Task PreviousMonth_ShowsItAsItEndedWithoutActions()
    {
        // Arrange
        using CultureScope culture = new();
        IRenderedComponent<BillsPage> cut = RenderPage();

        // Act
        cut.Find("button[aria-label='Previous month']").Click();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("September 2026");
            await Assert.That(cut.Markup).Contains("As it ended");
            await Assert.That(cut.FindAll("[aria-label='Actions for Energy']")).IsEmpty();
        }
    }

    [Test]
    public async Task SuggestionsWaiting_PointsToTheReviewPage()
    {
        // Arrange
        _billsService.GetSuggestionCountAsync(Arg.Any<CancellationToken>()).Returns(2);

        // Act
        IRenderedComponent<BillsPage> cut = RenderPage();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("2 repeat payments look like subscriptions");
            await Assert.That(cut.FindAll("a[href='Review/Subscriptions']")).Count().IsEqualTo(1);
            await Assert.That(_applicationState.SubscriptionsToReview).IsEqualTo(2);
        }
    }

    [Test]
    public async Task EndedBills_ListedOnTheirOwn()
    {
        // Arrange
        _billsService.GetBillsAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            BillTextTests.Bill(), BillTextTests.Bill("Old gym", endedOn: new DateOnly(2026, 8, 1))
        }.ToAsyncEnumerable());

        // Act
        IRenderedComponent<BillsPage> cut = RenderPage();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("Ended");
            await Assert.That(cut.Markup).Contains("Old gym");
        }
    }

    [Test]
    public async Task NoBills_SaysWhereTheyComeFrom()
    {
        // Arrange
        _billsService.GetBillsAsync(Arg.Any<CancellationToken>()).Returns(AsyncEnumerable.Empty<Bill>());
        _billsService.GetMonthAsync(2026, 10, Arg.Any<CancellationToken>())
            .Returns(new BillMonthOverview { Month = new DateOnly(2026, 10, 1), IsCurrent = true });

        // Act
        IRenderedComponent<BillsPage> cut = RenderPage();

        // Assert
        await Assert.That(cut.Markup).Contains("Your standing orders and direct debits show here after a sync");
    }

    private IRenderedComponent<BillsPage> RenderPage() =>
        Render<BillsPage>(p => p.AddCascadingValue(_applicationState));

    private static BillMonthOverview Overview(int month) => new()
    {
        Month = new DateOnly(2026, month, 1),
        IsCurrent = month == 10,
        Committed = 9.99m,
        Due = 9.99m,
        Paid = 9.99m,
        Bills =
        [
            new BillStatus
            {
                Bill = BillTextTests.Bill(),
                Periods =
                [
                    new BillPeriod
                    {
                        Due = new DateOnly(2026, month, 12), Expected = 9.99m, Paid = 9.99m, State = BillState.Paid,
                        PaidOn = new DateOnly(2026, month, 12)
                    }
                ]
            }
        ]
    };
}

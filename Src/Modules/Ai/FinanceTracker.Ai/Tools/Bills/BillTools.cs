using System.ComponentModel;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Dashboard;
using FinanceTracker.Contracts.Services;

namespace FinanceTracker.Ai.Tools.Bills;

/// <summary>
/// Tools for the user's bills and regular payments.
/// </summary>
/// <param name="billsService">Reads the bills, the same as the bills page.</param>
/// <param name="dashboardService">Reads the payments coming up, the same as the dashboard.</param>
/// <param name="timeProvider">The clock, whose time zone the days are in.</param>
/// <param name="pageLinks">Makes the links to the pages its results can be checked on.</param>
public sealed class BillTools(IBillsService billsService, IDashboardService dashboardService,
    TimeProvider timeProvider, PageLinks pageLinks)
{
    /// <summary>
    /// How many upcoming payments are returned when the model doesn't say.
    /// </summary>
    private const int DefaultCount = 10;

    /// <summary>
    /// The bills due in a month.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    [AiTool("get_bills")]
    [Description("Shows the user's bills, subscriptions and standing orders due in a month: what each was " +
        "expected to cost, what was paid, and which are late.")]
    public async Task<BillMonth> GetBillsAsync(
        [Description("The year, such as 2026.")] int year,
        [Description("The month, 1 to 12.")] int month,
        CancellationToken cancellationToken = default)
    {
        BillMonthOverview overview = await billsService.GetMonthAsync(year, month, cancellationToken);
        List<BillDue> payments = [];
        foreach (BillStatus status in overview.Bills)
        {
            foreach (BillPeriod period in status.Periods)
            {
                payments.Add(new BillDue(status.Bill.Name, status.Bill.Payee, period.Due, period.Expected,
                    period.Paid, period.State));
            }
        }

        List<BillDue> soonestFirst = payments
            .OrderBy(static x => x.Due)
            .Take(ToolText.MostRows)
            .ToList();
        return new BillMonth(overview.Start, overview.End, overview.Committed, overview.Due, overview.Paid,
            soonestFirst, ToolText.FromBanks, pageLinks.For(ToolLinks.Bills));
    }

    /// <summary>
    /// The next bill payments, only those due within <paramref name="days"/> when it's given.
    /// </summary>
    /// <param name="days">How many days ahead, null for no limit.</param>
    /// <param name="count">How many at most.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    [AiTool("get_upcoming_payments")]
    [Description("Lists the user's next bill payments, soonest first, including any that are late. Use days for " +
                 "questions like the next 10 days.")]
    public async Task<UpcomingPayments> GetUpcomingPaymentsAsync(
        [Description("Only payments due within this many days from today, late ones included. Leave out for the " +
                     "next ones whenever they're due.")]
        int? days = null,
        [Description("How many at most, 1 to 50. Defaults to 10.")] int count = DefaultCount,
        CancellationToken cancellationToken = default)
    {
        int take = Math.Clamp(count, 1, ToolText.MostRows);
        DateOnly? lastDay = LastDay(days);
        List<UpcomingBill> payments = [];
        await foreach (UpcomingPayment payment in dashboardService.GetUpcomingPaymentsAsync(take, cancellationToken))
        {
            if (lastDay is { } last && payment.Due > last && payment.State != BillState.Late)
            {
                continue;
            }

            payments.Add(new UpcomingBill(payment.PaymentName, payment.PaymentType, payment.Due, payment.Amount,
                payment.State));
        }

        return new UpcomingPayments(payments, pageLinks.For(ToolLinks.Bills));
    }

    /// <summary>
    /// The last day a payment can be due within <paramref name="days"/> from today, null for no limit.
    /// </summary>
    /// <param name="days">How many days ahead, null for no limit.</param>
    private DateOnly? LastDay(int? days)
    {
        if (days is not { } ahead)
        {
            return null;
        }

        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        return today.AddDays(Math.Max(ahead, 0));
    }
}

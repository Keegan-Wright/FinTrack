using FinanceTracker.Domain.PayPeriods;
using FinanceTracker.Generated.Attributes;
using FinanceTracker.Generated.Enums;
using FinanceTracker.Infrastructure.PayPeriods;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PayPeriod = FinanceTracker.Domain.PayPeriods.PayPeriod;

namespace FinanceTracker.Infrastructure.Exports.Statements;

/// <summary>
/// The pay periods there's a statement for today's back to the one the user's oldest transaction is in.
/// </summary>
[InjectionCategory(InjectionCategoryType.Service, ServiceDestinationType.Server)]
[Scoped<StatementPeriods>]
public sealed class StatementPeriods : ServiceBase<StatementPeriods>
{
    private readonly TimeProvider _timeProvider;

    public StatementPeriods(ICurrentUser currentUser,
        IDbContextFactory<FinanceTrackerContext> financeTrackerContextFactory, ILogger<StatementPeriods> logger,
        TimeProvider timeProvider) : base(currentUser, financeTrackerContextFactory, logger)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Newest first, just today's when the user has no transactions yet.
    /// </summary>
    public async Task<List<Contracts.PayPeriods.PayPeriod>> GetAsync(CancellationToken cancellationToken)
    {
        await using FinanceTrackerContext context =
            await FinanceTrackerContextFactory.CreateDbContextAsync(cancellationToken);

        DateOnly today = _timeProvider.Today;
        PayCalendar calendar = await PayCalendars.ForUserAsync(context, UserId, today, cancellationToken);
        DateOnly oldest = await OldestDayAsync(context, cancellationToken) ?? today;

        PayPeriod period = calendar.Containing(today);
        List<Contracts.PayPeriods.PayPeriod> periods = [PayPeriodsService.ToContract(period)];

        while (period.Start > oldest)
        {
            DateOnly before = new DateOnly(period.Year, period.Month, 1).AddMonths(-1);
            PayPeriod earlier = calendar.Named(before.Year, before.Month);

            // Each period starts before the one after it, this only guards against a calendar that doesn't
            if (earlier.Start >= period.Start)
            {
                break;
            }

            period = earlier;
            periods.Add(PayPeriodsService.ToContract(period));
        }

        return periods;
    }

    /// <summary>
    /// The day the user's oldest transaction happened where they are, null when they have none. The dates are
    /// encrypted, so they're read and compared here rather than in the database.
    /// </summary>
    private async Task<DateOnly?> OldestDayAsync(FinanceTrackerContext context, CancellationToken cancellationToken)
    {
        List<DateTime> occurred = await context.BankTransactions.OwnedBy(UserId)
            .Select(static x => x.OccurredAt)
            .ToListAsync(cancellationToken);

        if (occurred.Count == 0)
        {
            return null;
        }

        DateTime oldestUtc = DateTime.SpecifyKind(occurred.Min(), DateTimeKind.Utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(oldestUtc, _timeProvider.LocalTimeZone));
    }
}

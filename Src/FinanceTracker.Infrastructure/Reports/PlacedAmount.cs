using FinanceTracker.Domain.PayPeriods;

namespace FinanceTracker.Infrastructure.Reports;

/// <summary>
/// An amount with the day it fell on where the user is and the period that day is in, see
/// <see cref="ReportPeriods"/>.
/// </summary>
/// <param name="Amount">The amount.</param>
/// <param name="Day">The day it fell on where the user is.</param>
/// <param name="Period">The period that day is in.</param>
internal sealed record PlacedAmount(ReportAmount Amount, DateOnly Day, PayPeriod Period);

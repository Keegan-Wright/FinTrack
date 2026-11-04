using FinanceTracker.Domain.PayPeriods;

namespace FinanceTracker.Infrastructure.Reports;

/// <summary>
/// An amount with the day it fell on where the user is and the period that day is in, see
/// <see cref="ReportPeriods"/>.
/// </summary>
internal sealed record PlacedAmount(ReportAmount Amount, DateOnly Day, PayPeriod Period);

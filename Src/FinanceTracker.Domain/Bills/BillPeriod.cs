namespace FinanceTracker.Domain.Bills;

/// <summary>
/// One due date of a <see cref="Bill"/> and how it stands, from <see cref="BillCalculator.Periods"/>.
/// </summary>
/// <param name="Due">When it's due.</param>
/// <param name="Expected">What it was expected to cost on that date.</param>
/// <param name="State">How it stands.</param>
/// <param name="Paid">Money out less refunds across its payments, 0 when there are none.</param>
/// <param name="PaidOn">The day of its latest payment where the user is, null when there are none.</param>
/// <param name="PaymentIds">The transactions that paid it.</param>
public sealed record BillPeriod(
    DateOnly Due,
    decimal Expected,
    BillState State,
    decimal Paid,
    DateOnly? PaidOn,
    IReadOnlyList<Guid> PaymentIds);

using FinanceTracker.Contracts.Bills;

namespace FinanceTracker.Ai.Tools.Bills;

/// <summary>
/// A bill payment coming up.
/// </summary>
/// <param name="Name">The bill's name.</param>
/// <param name="Kind">Where the bill came from, such as a standing order or a subscription.</param>
/// <param name="Due">The day it's due.</param>
/// <param name="Amount">What it's expected to cost.</param>
/// <param name="State">Upcoming, or Late when it's more than a few days past due.</param>
public sealed record UpcomingBill(string Name, string Kind, DateOnly Due, decimal Amount, BillState State);

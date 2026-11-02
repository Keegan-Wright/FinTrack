namespace FinanceTracker.Client.Components.Transactions;

/// <summary>
/// A bill, debt or savings goal offered in the transaction filters.
/// </summary>
/// <param name="Id">Its id, sent in the filter.</param>
/// <param name="Name">How it reads in the list, a bill with its account and amount to tell same-named ones apart.</param>
public sealed record TransactionLinkFilter(Guid Id, string Name);

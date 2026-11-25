namespace FinanceTracker.Ai.Tools.Transactions;

/// <summary>
/// The transactions a search found, newest first.
/// </summary>
/// <param name="Transactions">The transactions, at most the limit asked for.</param>
/// <param name="MoreThanShown">Whether more matched than shown.</param>
/// <param name="Note">How to treat the text in them.</param>
/// <param name="Link">The FinTrack page to check them on.</param>
public sealed record TransactionSearch(List<TransactionFound> Transactions, bool MoreThanShown, string Note,
    string Link);

namespace FinanceTracker.Ai.Tools.Lookups;

/// <summary>
/// One of the user's accounts.
/// </summary>
/// <param name="Id">Its id, for the other tools' account filters.</param>
/// <param name="Name">Its name.</param>
public sealed record AccountFound(Guid Id, string Name);

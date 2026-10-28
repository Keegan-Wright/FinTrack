namespace FinanceTracker.Contracts.OpenBanking;

/// <summary>
/// One of the user's connected banks and how long its consent lasts.
/// </summary>
public sealed record BankConnectionSummary
{
    /// <summary>
    /// The connection's id, for <see cref="Services.IOpenBankingService.GetRenewalUrlAsync"/>.
    /// </summary>
    public required Guid ConnectionId { get; init; }

    /// <summary>
    /// The bank's name as shown to users.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The bank's logo image, stored when it was connected.
    /// </summary>
    public required byte[] Logo { get; init; }

    /// <summary>
    /// When the consent ends, as the provider reports it.
    /// </summary>
    public required DateTime ConsentExpiresAt { get; init; }

    /// <summary>
    /// Whether the consent needs renewing, worked out from <see cref="ConsentExpiresAt"/>.
    /// </summary>
    public required ConsentState ConsentState { get; init; }
}

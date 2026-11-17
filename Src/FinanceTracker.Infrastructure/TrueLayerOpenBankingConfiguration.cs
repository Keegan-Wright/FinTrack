namespace FinanceTracker.Infrastructure;

/// <summary>
/// TrueLayer's settings, from the <c>OPEN_BANKING_TRUELAYER_*</c> environment variables.
/// </summary>
public sealed class TrueLayerOpenBankingConfiguration
{
    /// <summary>
    /// TrueLayer's auth API, where users connect their banks.
    /// </summary>
    public required Uri BaseAuthUrl { get; init; }

    /// <summary>
    /// TrueLayer's data API.
    /// </summary>
    public required Uri BaseDataUrl { get; init; }

    /// <summary>
    /// Where TrueLayer sends the user back to once they've connected a bank.
    /// </summary>
    public required Uri AuthRedirectUrl { get; init; }

    /// <summary>
    /// FinTrack's client id at TrueLayer.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// FinTrack's client secret at TrueLayer.
    /// </summary>
    public required string ClientSecret { get; init; }

    /// <summary>
    /// The address sent to TrueLayer as the user's, in the <c>x-PSU-IP</c> header.
    /// </summary>
    public required string PublicIpAddress { get; init; }
}

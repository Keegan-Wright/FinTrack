namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// What the user adds to their authenticator app to turn on two-factor sign in.
/// </summary>
public sealed class AuthenticatorSetup
{
    /// <summary>
    /// The key in groups of four, for typing into the app.
    /// </summary>
    public required string SharedKey { get; init; }

    /// <summary>
    /// The key as a QR code for the app to scan, as SVG markup.
    /// </summary>
    public required string QrCodeSvg { get; init; }
}

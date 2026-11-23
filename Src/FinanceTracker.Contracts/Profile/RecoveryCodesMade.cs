namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// The change was made, and with it new recovery codes.
/// </summary>
/// <param name="RecoveryCodes">The codes, shown to the user this once.</param>
public sealed record RecoveryCodesMade(IReadOnlyList<string> RecoveryCodes) : TwoFactorChange;

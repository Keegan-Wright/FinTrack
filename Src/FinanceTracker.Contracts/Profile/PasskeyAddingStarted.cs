namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// Adding a passkey can go ahead, once the browser has made it.
/// </summary>
/// <param name="CreationOptionsJson">What the browser needs to make the passkey, as WebAuthn JSON.</param>
public sealed record PasskeyAddingStarted(string CreationOptionsJson) : PasskeyChange;

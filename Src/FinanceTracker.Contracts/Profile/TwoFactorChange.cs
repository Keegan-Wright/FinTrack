using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// How a change to the user's two-factor sign in went. One of <see cref="TwoFactorChangeRefused"/>, <see
/// cref="TwoFactorChangeMade"/> or <see cref="RecoveryCodesMade"/>, so a caller handles each.
/// </summary>
[JsonPolymorphic(InferClosedTypePolymorphism = true)]
public closed record TwoFactorChange;

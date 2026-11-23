using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// How a change to the user's passkeys went. One of <see cref="PasskeyChangeRefused"/>, <see cref="PasskeyChangeMade"/>
/// or <see cref="PasskeyAddingStarted"/>, so a caller handles each.
/// </summary>
[JsonPolymorphic(InferClosedTypePolymorphism = true)]
public closed record PasskeyChange;

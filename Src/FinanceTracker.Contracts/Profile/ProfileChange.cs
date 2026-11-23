using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts.Profile;

/// <summary>
/// How a change to the user's own account went. One of <see cref="ProfileChangeRefused"/>, <see
/// cref="ProfileChangeMade"/> or <see cref="EmailChangeLinkSent"/>, so a caller handles each.
/// </summary>
[JsonPolymorphic(InferClosedTypePolymorphism = true)]
public closed record ProfileChange;

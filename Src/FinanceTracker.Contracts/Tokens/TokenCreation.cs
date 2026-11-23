using System.Text.Json.Serialization;

namespace FinanceTracker.Contracts.Tokens;

/// <summary>
/// How making a personal access token went. One of <see cref="TokenCreationRefused"/> or <see cref="TokenCreated"/>, so
/// a caller handles each.
/// </summary>
[JsonPolymorphic(InferClosedTypePolymorphism = true)]
public closed record TokenCreation;

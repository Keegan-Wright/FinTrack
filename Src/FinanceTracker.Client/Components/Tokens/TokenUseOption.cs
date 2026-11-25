namespace FinanceTracker.Client.Components.Tokens;

/// <summary>
/// One thing a new token can be for, offered in <see cref="NewTokenDialog"/>.
/// </summary>
/// <param name="Value">What's chosen.</param>
/// <param name="Label">What the user reads.</param>
public sealed record TokenUseOption(string Value, string Label);

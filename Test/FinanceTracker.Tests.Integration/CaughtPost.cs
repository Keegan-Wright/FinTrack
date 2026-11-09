namespace FinanceTracker.Tests.Integration;

/// <summary>
/// A post the app made to a webhook, as the <see cref="WebhookCatcher"/> caught it.
/// </summary>
public sealed record CaughtPost(IReadOnlyDictionary<string, string> Headers, byte[] Body);

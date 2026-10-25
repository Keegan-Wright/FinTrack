namespace FinanceTracker.Client.Components.Dashboard;

/// <summary>
/// One bar of <see cref="TopCategories"/>.
/// </summary>
/// <param name="Tag">The tag, as shown on the axis.</param>
/// <param name="Total">Money out on transactions carrying the tag, positive.</param>
public sealed record TagSpend(string Tag, decimal Total);

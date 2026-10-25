using System.Collections.Generic;

namespace FinanceTracker.Generated.DependencyInjection;

/// <summary>
/// A class to register as <see cref="ServiceType"/>.
/// </summary>
/// <param name="Category">The <c>InjectionCategoryType</c> member's name.</param>
/// <param name="Destinations">The names of the <c>ServiceDestinationType</c> flags set.</param>
internal sealed class InjectableType(
    string implementationType,
    string serviceType,
    string lifetime,
    string category,
    IReadOnlyCollection<string> destinations)
{
    public string ImplementationType { get; } = implementationType;
    public string ServiceType { get; } = serviceType;
    public string Lifetime { get; } = lifetime;
    public string Category { get; } = category;
    public IReadOnlyCollection<string> Destinations { get; } = destinations;
}

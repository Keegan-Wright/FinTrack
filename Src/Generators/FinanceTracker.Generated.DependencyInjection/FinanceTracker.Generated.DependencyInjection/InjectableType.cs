using System.Collections.Generic;

namespace FinanceTracker.Generated.DependencyInjection;

/// <summary>
/// A class to register as <see cref="ServiceType"/>.
/// </summary>
/// <param name="implementationType">The class, fully qualified.</param>
/// <param name="serviceType">What it's registered as, fully qualified.</param>
/// <param name="lifetime">Its lifetime, <c>Scoped</c>, <c>Transient</c> or <c>Singleton</c>.</param>
/// <param name="category">The <c>InjectionCategoryType</c> member's name.</param>
/// <param name="destinations">The names of the <c>ServiceDestinationType</c> flags set.</param>
internal sealed class InjectableType(
    string implementationType,
    string serviceType,
    string lifetime,
    string category,
    IReadOnlyCollection<string> destinations)
{
    /// <summary>
    /// The class, fully qualified.
    /// </summary>
    public string ImplementationType { get; } = implementationType;

    /// <summary>
    /// What it's registered as, fully qualified.
    /// </summary>
    public string ServiceType { get; } = serviceType;

    /// <summary>
    /// Its lifetime, <c>Scoped</c>, <c>Transient</c> or <c>Singleton</c>.
    /// </summary>
    public string Lifetime { get; } = lifetime;

    /// <summary>
    /// The <c>InjectionCategoryType</c> member's name.
    /// </summary>
    public string Category { get; } = category;

    /// <summary>
    /// The names of the <c>ServiceDestinationType</c> flags set.
    /// </summary>
    public IReadOnlyCollection<string> Destinations { get; } = destinations;
}

using System.Reflection;
using FinanceTracker.Client;

namespace FinanceTracker.Extensions;

/// <summary>
/// The assemblies holding pages the server routes to and prerenders, besides its own.
/// </summary>
public static class RoutedAssemblies
{
    /// <summary>
    /// The client's, and those of any modules built in.
    /// </summary>
    public static Assembly[] All { get; } =
    [
        typeof(Routes).Assembly,
#if FINTRACK_AI
        typeof(Ai.Client.AiPage).Assembly
#endif
    ];
}

using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
#if FINTRACK_AI
using FinanceTracker.Client.Ai;
#endif

namespace FinanceTracker.Client;

/// <summary>
/// Routes the client's pages, and the pages of modules built in that the browser loads when first needed.
/// </summary>
public partial class Routes
{
    /// <summary>
    /// Assemblies loaded after the app started, whose pages the router also finds.
    /// </summary>
    private readonly List<Assembly> _lazyLoadedAssemblies = [];

#if FINTRACK_AI
    /// <summary>
    /// Loads the AI module.
    /// </summary>
    [Inject]
    private AiModuleLoader AiModuleLoader { get; set; } = null!;

    /// <summary>
    /// Loads a module before navigating to one of its pages.
    /// </summary>
    private EventCallback<NavigationContext> OnNavigate => EventCallback.Factory.Create<NavigationContext>(this,
        LoadModuleForAsync);

    /// <summary>
    /// Loads the AI module when <paramref name="context"/> goes to one of its pages.
    /// </summary>
    /// <param name="context">Where the router is going.</param>
    private async Task LoadModuleForAsync(NavigationContext context)
    {
        if (!AiModuleLoader.IsModulePage(context.Path))
        {
            return;
        }

        Assembly assembly = await AiModuleLoader.LoadAsync();
        // The router expects a cancelled navigation to throw, or it may render a page it has moved on from
        context.CancellationToken.ThrowIfCancellationRequested();
        if (!_lazyLoadedAssemblies.Contains(assembly))
        {
            _lazyLoadedAssemblies.Add(assembly);
        }
    }
#else
    /// <summary>
    /// Nothing to load before navigating, no modules are built in.
    /// </summary>
    private static EventCallback<NavigationContext> OnNavigate => default;
#endif
}

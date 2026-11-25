#if FINTRACK_AI
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Services;

namespace FinanceTracker.Client.Ai;

/// <summary>
/// Loads the AI module's browser assembly once, for its pages and its app bar button. Only built with
/// FinTrackAi=true. On the server, while prerendering, the assembly is already there and loading just finds it.
/// </summary>
/// <param name="lazyAssemblyLoader">Fetches assemblies the browser didn't load at start.</param>
public sealed class AiModuleLoader(LazyAssemblyLoader lazyAssemblyLoader)
{
    /// <summary>
    /// The module's browser assembly, as the browser fetches it.
    /// </summary>
    private const string AssemblyFile = "FinanceTracker.Ai.Client.wasm";

    /// <summary>
    /// The module's app bar button, by name, since the app can't refer to its type.
    /// </summary>
    private const string AppBarButtonType = "FinanceTracker.Ai.Client.AiAppBarButton";

    /// <summary>
    /// The address the module's pages live under, without the leading slash.
    /// </summary>
    private const string PagePath = "ai";

    private Task<Assembly>? _loading;

    /// <summary>
    /// The module's assembly once loaded, null before.
    /// </summary>
    public Assembly? Assembly { get; private set; }

    /// <summary>
    /// The module's app bar button once loaded, null before.
    /// </summary>
    public Type? AppBarButton => Assembly?.GetType(AppBarButtonType, throwOnError: true);

    /// <summary>
    /// Whether <paramref name="path"/> is one of the module's pages.
    /// </summary>
    /// <param name="path">The address being navigated to, without the leading slash, as the router gives it.</param>
    public static bool IsModulePage(string path) =>
        path.Equals(PagePath, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith($"{PagePath}/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Loads the assembly when the app is opening on one of the module's pages.
    /// </summary>
    /// <param name="navigation">Where the app is opening.</param>
    public async Task LoadIfOpeningModulePageAsync(NavigationManager navigation)
    {
        string path = navigation.ToBaseRelativePath(navigation.Uri).Split('?', '#')[0];
        if (IsModulePage(path))
        {
            await LoadAsync();
        }
    }

    /// <summary>
    /// Loads the assembly, once however often it's asked for.
    /// </summary>
    public Task<Assembly> LoadAsync() => _loading ??= LoadOnceAsync();

    /// <summary>
    /// Fetches and loads the assembly.
    /// </summary>
    private async Task<Assembly> LoadOnceAsync()
    {
        IEnumerable<Assembly> loaded = await lazyAssemblyLoader.LoadAssembliesAsync([AssemblyFile]);
        Assembly = loaded.Single();
        return Assembly;
    }
}
#endif

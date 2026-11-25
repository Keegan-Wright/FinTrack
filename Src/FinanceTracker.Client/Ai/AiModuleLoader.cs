#if FINTRACK_AI
using System.Reflection;
using FinanceTracker.Client.Common.Modules;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Services;

namespace FinanceTracker.Client.Ai;

/// <summary>
/// Loads the AI module's browser assembly once, for its pages and the components it shows in the app's slots. Only
/// built with FinTrackAi=true. On the server, while prerendering, the assembly is already there and loading just finds
/// it.
/// </summary>
/// <param name="lazyAssemblyLoader">Fetches assemblies the browser didn't load at start.</param>
public sealed class AiModuleLoader(LazyAssemblyLoader lazyAssemblyLoader)
{
    /// <summary>
    /// The module's browser assembly, as the browser fetches it.
    /// </summary>
    private const string AssemblyFile = "FinanceTracker.Ai.Client.wasm";

    /// <summary>
    /// The address the module's pages live under, without the leading slash.
    /// </summary>
    private const string PagePath = "ai";

    private Task<Assembly>? _loading;

    /// <summary>
    /// Raised once the assembly has loaded, so the slots can show the module's components.
    /// </summary>
    public event Action? Loaded;

    /// <summary>
    /// The module's assembly once loaded, null before.
    /// </summary>
    public Assembly? Assembly { get; private set; }

    /// <summary>
    /// Whether <paramref name="path"/> is one of the module's pages.
    /// </summary>
    /// <param name="path">The address being navigated to, without the leading slash, as the router gives it.</param>
    public static bool IsModulePage(string path) =>
        path.Equals(PagePath, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith($"{PagePath}/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The module's components for <paramref name="slot"/>, none before it has loaded.
    /// </summary>
    /// <param name="slot">The place in the app, one of <see cref="ModuleSlots"/>.</param>
    public IReadOnlyList<Type> ComponentsFor(string slot)
    {
        if (Assembly is null)
        {
            return [];
        }

        return Assembly.GetExportedTypes()
            .Where(type => type.GetCustomAttributes<ModuleSlotAttribute>().Any(x => x.Slot == slot))
            .OrderBy(static type => type.Name, StringComparer.Ordinal)
            .ToList();
    }

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
    /// Fetches and loads the assembly, then tells the slots.
    /// </summary>
    private async Task<Assembly> LoadOnceAsync()
    {
        IEnumerable<Assembly> loaded = await lazyAssemblyLoader.LoadAssembliesAsync([AssemblyFile]);
        Assembly = loaded.Single();
        Loaded?.Invoke();
        return Assembly;
    }
}
#endif

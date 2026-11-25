using FinanceTracker.Client.Common.Modules;
using Microsoft.AspNetCore.Components;
#if FINTRACK_AI
using FinanceTracker.Client.Ai;
#endif

namespace FinanceTracker.Client.Components;

/// <summary>
/// Shows the components a module built in puts in one of the app's places, see
/// <see cref="ModuleSlots"/>.
/// </summary>
#if FINTRACK_AI
public partial class ModuleSlot : IDisposable
#else
public partial class ModuleSlot
#endif
{
    /// <summary>
    /// Which place this is, one of <see cref="ModuleSlots"/>.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public required string Name { get; set; }

#if FINTRACK_AI
    /// <summary>
    /// Loads the AI module and finds its components.
    /// </summary>
    [Inject]
    private AiModuleLoader AiModuleLoader { get; set; } = null!;

    /// <summary>
    /// The module's components for this place, none until it has loaded.
    /// </summary>
    private IReadOnlyList<Type> Components => AiModuleLoader.ComponentsFor(Name);

    /// <inheritdoc/>
    protected override void OnInitialized() => AiModuleLoader.Loaded += OnModuleLoaded;

    /// <inheritdoc/>
    public void Dispose()
    {
        AiModuleLoader.Loaded -= OnModuleLoaded;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Shows the module's components once it has loaded.
    /// </summary>
    private void OnModuleLoaded() => _ = InvokeAsync(StateHasChanged);
#else
    /// <summary>
    /// None, no modules are built in.
    /// </summary>
    private static IReadOnlyList<Type> Components => [];
#endif
}

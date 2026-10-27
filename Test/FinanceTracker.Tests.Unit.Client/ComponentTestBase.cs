using Bunit;
using MudBlazor.Services;

namespace FinanceTracker.Tests.Unit.Client;

/// <summary>
/// A bUnit context per test with MudBlazor's services. JS interop is loose, so
/// MudBlazor's calls return defaults and a test only sets up the calls it checks.
/// </summary>
/// <remarks>
/// Amounts and dates render in the current culture, wrap the render in a <see cref="CultureScope"/>.
/// </remarks>
public abstract class ComponentTestBase : BunitContext
{
    protected ComponentTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }
}

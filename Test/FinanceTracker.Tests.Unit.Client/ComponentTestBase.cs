using Bunit;
using FinanceTracker.Tests.Unit.Shared;
using MudBlazor.Services;

namespace FinanceTracker.Tests.Unit.Client;

/// <summary>
/// A bUnit context per test with MudBlazor's services. JS interop is loose, so
/// MudBlazor's calls return defaults and a test only sets up the calls it checks. bUnit's waits get
/// <see cref="TestTimeouts.Wait"/> rather than its own second, which a busy parallel run can miss.
/// </summary>
/// <remarks>
/// Amounts and dates render in the current culture, wrap the render in a <see cref="CultureScope"/>.
/// </remarks>
public abstract class ComponentTestBase : BunitContext
{
    protected ComponentTestBase()
    {
        DefaultWaitTimeout = TestTimeouts.Wait;
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }
}

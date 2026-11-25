using Bunit;
#if FINTRACK_AI
using FinanceTracker.Client.Ai;
using Microsoft.AspNetCore.Components.WebAssembly.Services;
#endif
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

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

        // The money pages' Everyone or Just mine switch, hidden as nothing's shared. A test about sharing registers
        // its own, which wins
        ISharingService sharing = Substitute.For<ISharingService>();
        sharing.GetViewAsync(Arg.Any<CancellationToken>())
            .Returns(new SharingView { IncludesShared = true, HasSharedAccounts = false });
        Services.AddSingleton(sharing);

#if FINTRACK_AI
        // The layout loads the AI module in the AI build. Outside the browser loading finds the assembly the tests
        // already reference
        Services.AddSingleton<LazyAssemblyLoader>();
        Services.AddSingleton<AiModuleLoader>();
#endif
    }
}

using System.Reflection;
using FinanceTracker.Client.Layout;
using FinanceTracker.Extensions;

namespace FinanceTracker.Tests.Unit.FinanceTracker;

/// <summary>
/// The AI module is only in builds made with FinTrackAi=true. The core's CI runs the plain build's half, showing it
/// has none of the module, and the module's CI runs the AI build's half by its category, showing it has all of it.
/// </summary>
public class AiModuleBuildTests
{
#if FINTRACK_AI
    [Test]
    [Category("AiModule")]
    public async Task Host_InTheAiBuild_ReferencesTheAiModule() =>
        await Assert.That(AiReferences(typeof(Program).Assembly)).Contains("FinanceTracker.Ai");

    [Test]
    [Category("AiModule")]
    public async Task RoutedAssemblies_InTheAiBuild_IncludeTheAiPages() =>
        await Assert.That(RoutedAssemblies.All.Select(static x => x.GetName().Name ?? string.Empty))
            .Contains("FinanceTracker.Ai.Client");

    [Test]
    [Category("AiModule")]
    public async Task AiPages_DoNotReferenceServerOnlyAssemblies() =>
        await Assert.That(typeof(Ai.Client.AiPage).Assembly.GetReferencedAssemblies()
                .Select(static x => x.Name ?? string.Empty)
                .Where(static name => name.StartsWith("FinanceTracker.Infrastructure", StringComparison.Ordinal) ||
                                      name.StartsWith("FinanceTracker.Domain", StringComparison.Ordinal) ||
                                      name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)))
            .IsEmpty();
#else
    [Test]
    public async Task Host_InThePlainBuild_HasNoAiModule() =>
        await Assert.That(AiReferences(typeof(Program).Assembly)).IsEmpty();

    [Test]
    public async Task Client_InThePlainBuild_HasNoAiModule() =>
        await Assert.That(AiReferences(typeof(LayoutBase).Assembly)).IsEmpty();

    [Test]
    public async Task RoutedAssemblies_InThePlainBuild_AreOnlyTheClients() =>
        await Assert.That(RoutedAssemblies.All.Select(static x => x.GetName().Name ?? string.Empty))
            .IsEquivalentTo(["FinanceTracker.Client"]);
#endif

    private static List<string> AiReferences(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(static x => x.Name ?? string.Empty)
            .Where(static name => name.StartsWith("FinanceTracker.Ai", StringComparison.Ordinal))
            .ToList();
}

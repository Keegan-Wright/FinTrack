using System.Reflection;
using FinanceTracker.Client;
using FinanceTracker.Contracts.Services;

namespace FinanceTracker.Tests.Unit.FinanceTracker;

/// <summary>
/// The front end will run in the browser, so it and the contracts it shares with the server must not depend on
/// server only code.
/// </summary>
public class FrontEndBoundaryTests
{
    private static readonly string[] ServerOnlyAssemblyPrefixes =
    [
        "FinanceTracker.Domain",
        "FinanceTracker.Infrastructure",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore.Identity",
        "Npgsql",
        "TickerQ"
    ];

    [Test]
    public async Task Client_DoesNotReferenceServerOnlyAssemblies() =>
        await Assert.That(ServerOnlyReferences(typeof(ApplicationState).Assembly)).IsEmpty();

    [Test]
    public async Task Contracts_DoNotReferenceServerOnlyAssemblies() =>
        await Assert.That(ServerOnlyReferences(typeof(IAccountService).Assembly)).IsEmpty();

    private static List<string> ServerOnlyReferences(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(static x => x.Name ?? string.Empty)
            .Where(static name => ServerOnlyAssemblyPrefixes.Any(prefix =>
                name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();
}

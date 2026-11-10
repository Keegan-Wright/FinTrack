using FinanceTracker.AppHost.ServiceDefaults;
using Microsoft.AspNetCore.Http;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Telemetry;

public class TraceFiltersTests
{
    [Test]
    [Arguments("/")]
    [Arguments("/Transactions")]
    [Arguments("/Account/Login")]
    [Arguments("/api/budgets")]
    [Arguments("/hubs/sync/negotiate")]
    public async Task IsTraced_PageApiOrHub_IsTraced(string path) =>
        await Assert.That(TraceFilters.IsTraced(Request(path))).IsTrue();

    [Test]
    [Arguments("/health")]
    [Arguments("/alive")]
    [Arguments("/_framework/dotnet.wasm")]
    [Arguments("/_content/MudBlazor/MudBlazor.min.css")]
    [Arguments("/app.css")]
    [Arguments("/js/app.js")]
    [Arguments("/favicon.png")]
    public async Task IsTraced_HealthCheckOrFile_IsLeftOut(string path) =>
        await Assert.That(TraceFilters.IsTraced(Request(path))).IsFalse();

    private static DefaultHttpContext Request(string path) => new() { Request = { Path = path } };
}

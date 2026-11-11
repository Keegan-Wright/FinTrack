using FinanceTracker.AppHost.ServiceDefaults;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Telemetry;

public class ServiceVersionTests
{
    [Test]
    [Arguments("0.1.6+3e871e2a9c", "v0.1.6")]
    [Arguments("0.1.6", "v0.1.6")]
    [Arguments("0.0.0-dev+3e871e2a9c", "v0.0.0-dev")]
    public async Task From_DropsTheCommit_SoEachReleaseIsOneVersion(string informationalVersion, string expected) =>
        await Assert.That(ServiceVersion.From(informationalVersion)).IsEqualTo(expected);

    [Test]
    [Arguments(null)]
    [Arguments("")]
    public async Task From_NoVersion_IsUnknown(string? informationalVersion) =>
        await Assert.That(ServiceVersion.From(informationalVersion)).IsEqualTo("unknown");

    [Test]
    public async Task Current_IsTheRunningBuildsVersion() =>
        await Assert.That(ServiceVersion.Current).StartsWith("v");
}

using FinanceTracker.Client.Layout;

namespace FinanceTracker.Tests.Unit.Client.Layout;

public class AppVersionTests
{
    [Test]
    [Arguments("0.1.1+3e871e276a9a040734636c7c913890812f1c6de0", "v0.1.1 · 3e871e2")]
    [Arguments("0.1.1", "v0.1.1")]
    [Arguments("0.0.0-dev+abc", "v0.0.0-dev · abc")]
    [Arguments("0.1.1+", "v0.1.1")]
    public async Task Format_ShowsTheVersionAndShortCommit(string informationalVersion, string expected)
    {
        // Act
        string display = AppVersion.Format(informationalVersion);

        // Assert
        await Assert.That(display).IsEqualTo(expected);
    }
}

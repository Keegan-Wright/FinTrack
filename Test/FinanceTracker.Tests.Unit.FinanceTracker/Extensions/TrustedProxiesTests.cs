using System.Net;
using FinanceTracker.Extensions;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Extensions;

public class TrustedProxiesTests
{
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("  ")]
    public async Task Parse_NotSet_TrustsNone(string? value) =>
        await Assert.That(TrustedProxies.Parse(value)).IsEmpty();

    [Test]
    public async Task Parse_NetworksAndAddresses_EachAsANetwork()
    {
        IReadOnlyList<IPNetwork> networks = TrustedProxies.Parse(" 172.18.0.0/16, 10.0.0.5 ,fd00::/8 ");

        using (Assert.Multiple())
        {
            await Assert.That(networks.Select(static x => x.ToString()))
                .IsEquivalentTo(["172.18.0.0/16", "10.0.0.5/32", "fd00::/8"]);
            await Assert.That(networks[0].Contains(IPAddress.Parse("172.18.4.2"))).IsTrue();
            await Assert.That(networks[1].Contains(IPAddress.Parse("10.0.0.6"))).IsFalse();
        }
    }

    [Test]
    [Arguments("proxy.local")]
    [Arguments("172.18.0.0/16, 300.1.1.1")]
    public async Task Parse_SomethingElse_SaysWhich(string value) =>
        await Assert.That(() => TrustedProxies.Parse(value)).Throws<FormatException>();
}

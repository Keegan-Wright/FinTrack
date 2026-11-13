using FinanceTracker.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Tests.Unit.FinanceTracker.Extensions;

/// <summary>
/// Which forwarded headers the app reads with ASP.NET Core's own <c>FORWARDEDHEADERS_ENABLED</c> on, as Aspire runs
/// it, which on its own trusts every sender.
/// </summary>
public class ForwardedHeaderExtensionsTests
{
    private const string BuiltInOn = "--FORWARDEDHEADERS_ENABLED=true";

    [Test]
    public async Task BuiltInOnItsOwn_TrustsEverySender()
    {
        // The switch on, nothing of the app's, to show it's what the other tests overrule
        ForwardedHeadersOptions options = OptionsOf(BuiltInOn, addTheApps: false);

        using (Assert.Multiple())
        {
            await Assert.That(options.ForwardedHeaders).IsNotEqualTo(ForwardedHeaders.None);
            await Assert.That(options.KnownIPNetworks.Count + options.KnownProxies.Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task BuiltInOn_TrustedProxiesSet_TrustsOnlyThem()
    {
        ForwardedHeadersOptions options = OptionsOf(BuiltInOn, "--TRUSTED_PROXIES=10.255.0.0/16");

        using (Assert.Multiple())
        {
            await Assert.That(options.ForwardedHeaders)
                .IsEqualTo(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
            await Assert.That(options.KnownIPNetworks.Select(static x => x.ToString()))
                .IsEquivalentTo(["10.255.0.0/16"]);
            await Assert.That(options.KnownProxies).IsEmpty();
        }
    }

    [Test]
    public async Task BuiltInOn_NoTrustedProxies_ReadsNoForwardedHeaders()
    {
        ForwardedHeadersOptions options = OptionsOf(BuiltInOn);

        await Assert.That(options.ForwardedHeaders).IsEqualTo(ForwardedHeaders.None);
    }

    private static ForwardedHeadersOptions OptionsOf(params string[] args) => OptionsOf(args, addTheApps: true);

    private static ForwardedHeadersOptions OptionsOf(string arg, bool addTheApps) => OptionsOf([arg], addTheApps);

    private static ForwardedHeadersOptions OptionsOf(string[] args, bool addTheApps)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        if (addTheApps)
        {
            builder.AddFinTrackForwardedHeaders();
        }

        using WebApplication app = builder.Build();
        return app.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }
}

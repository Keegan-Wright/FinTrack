using System.Net;
using FinanceTracker.AppHost.ServiceDefaults;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace FinanceTracker.Extensions;

public static class ForwardedHeaderExtensions
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// Behind a reverse proxy, takes the client's address and whether it came over HTTPS from the proxy's
        /// forwarded headers, but only from the proxies <see cref="FinTrackEnvironment.TrustedProxies"/> names. When
        /// none are named, no forwarded header is read, so a request straight to the app can't claim another address.
        /// </summary>
        /// <remarks>
        /// After every other setting, as ASP.NET Core's own <c>ASPNETCORE_FORWARDEDHEADERS_ENABLED</c>, which Aspire
        /// turns on for the projects it runs, trusts every source. With no proxies listed the middleware trusts any
        /// sender too, so none named turns forwarded headers off rather than leaving the list empty.
        /// </remarks>
        /// <exception cref="FormatException">A trusted proxy isn't an address or a network.</exception>
        public WebApplicationBuilder AddFinTrackForwardedHeaders()
        {
            IReadOnlyList<IPNetwork> proxies =
                TrustedProxies.Parse(builder.Configuration[FinTrackEnvironment.TrustedProxies]);

            builder.Services.PostConfigure<ForwardedHeadersOptions>(options => TrustOnly(options, proxies));
            return builder;
        }
    }

    /// <summary>
    /// Reads forwarded headers from <paramref name="proxies"/> and no one else, or none at all when there are none.
    /// </summary>
    private static void TrustOnly(ForwardedHeadersOptions options, IReadOnlyList<IPNetwork> proxies)
    {
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        if (proxies.Count == 0)
        {
            options.ForwardedHeaders = ForwardedHeaders.None;
            return;
        }

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        foreach (IPNetwork proxy in proxies)
        {
            options.KnownIPNetworks.Add(proxy);
        }
    }

    extension(WebApplication app)
    {
        /// <summary>
        /// Reads the trusted proxies' forwarded headers, first, so everything after sees the client's address and
        /// HTTPS. Does nothing when no proxy is trusted, see <see cref="AddFinTrackForwardedHeaders"/>.
        /// </summary>
        public WebApplication UseFinTrackForwardedHeaders()
        {
            if (!string.IsNullOrWhiteSpace(app.Configuration[FinTrackEnvironment.TrustedProxies]))
            {
                app.UseForwardedHeaders();
            }

            return app;
        }
    }
}

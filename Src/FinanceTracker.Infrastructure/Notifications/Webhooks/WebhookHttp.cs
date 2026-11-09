using System.Net.Sockets;

namespace FinanceTracker.Infrastructure.Notifications.Webhooks;

/// <summary>
/// The HTTP handler webhooks are posted through. It looks the address up itself and connects only to addresses
/// <see cref="WebhookSettings"/> allows, so a name that points at the private network, now or by the time it's
/// posted to, is refused. No redirects, which could lead anywhere, and no proxy, which would connect for it.
/// </summary>
public static class WebhookHttp
{
    public static SocketsHttpHandler CreateHandler(WebhookSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            ConnectCallback = (context, cancellationToken) => ConnectAsync(context, settings, cancellationToken)
        };
    }

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context,
        WebhookSettings settings, CancellationToken cancellationToken)
    {
        System.Net.IPAddress[] allowed =
            await WebhookAddresses.AllowedAsync(context.DnsEndPoint.Host, settings, cancellationToken);
        if (allowed.Length == 0)
        {
            throw new WebhookAddressRefusedException();
        }

        // Dual mode, so it connects to IPv4 and IPv6 addresses alike
        Socket socket = new(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}

using System.Net;
using System.Net.Sockets;

namespace FinanceTracker.Infrastructure.Notifications.Webhooks;

/// <summary>
/// Which addresses a webhook may go to. Checked when the address is saved, for a quick answer, and again on every
/// connection, see <see cref="WebhookHttp"/>, as a name can point somewhere else by the time it's posted to.
/// </summary>
public static class WebhookAddresses
{
    /// <summary>
    /// Why posts don't go to private addresses, in words for the user.
    /// </summary>
    public const string PrivateRefusal =
        "This FinTrack doesn't post to addresses on a private network. Whoever runs it can allow them.";

    /// <summary>
    /// Networks that aren't the public internet, private ranges, this machine, link local, shared address space
    /// (carrier NAT), "this network", multicast and the reserved and broadcast range, and their IPv6 equivalents.
    /// </summary>
    private static readonly IPNetwork[] NotPublic =
    [
        IPNetwork.Parse("0.0.0.0/8"),
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("100.64.0.0/10"),
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("224.0.0.0/3"),
        IPNetwork.Parse("::/128"),
        IPNetwork.Parse("::1/128"),
        IPNetwork.Parse("fc00::/7"),
        IPNetwork.Parse("fe80::/10"),
        IPNetwork.Parse("ff00::/8")
    ];

    /// <summary>
    /// Whether <paramref name="address"/> is on the public internet. An IPv4 address written as IPv6 is judged as
    /// IPv4.
    /// </summary>
    public static bool IsPublic(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        IPAddress judged = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return !NotPublic.Any(x => x.BaseAddress.AddressFamily == judged.AddressFamily && x.Contains(judged));
    }

    public static bool IsAllowed(IPAddress address, WebhookSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.AllowPrivateAddresses || IsPublic(address);
    }

    /// <summary>
    /// Why <paramref name="url"/> won't be posted to, as far as can be told without looking its name up: an address
    /// written as a private IP, or this machine by name. Null when it may be.
    /// </summary>
    public static string? RefusalFor(Uri url, WebhookSettings settings)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.AllowPrivateAddresses)
        {
            return null;
        }

        if (url.IsLoopback)
        {
            return PrivateRefusal;
        }

        if (IPAddress.TryParse(url.IdnHost, out IPAddress? address) && !IsPublic(address))
        {
            return PrivateRefusal;
        }

        return null;
    }

    /// <summary>
    /// The addresses of <paramref name="host"/> a webhook may connect to, none when every one is refused.
    /// </summary>
    internal static async Task<IPAddress[]> AllowedAsync(string host, WebhookSettings settings,
        CancellationToken cancellationToken)
    {
        IPAddress[] addresses = IPAddress.TryParse(host, out IPAddress? literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);

        return addresses
            .Where(x => x.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
            .Where(x => IsAllowed(x, settings))
            .ToArray();
    }
}

using System.Net;

namespace FinanceTracker.Extensions;

/// <summary>
/// The reverse proxies whose forwarded headers are trusted, from
/// <see cref="AppHost.ServiceDefaults.FinTrackEnvironment.TrustedProxies"/>. Addresses or networks, comma separated,
/// e.g. <c>172.18.0.0/16, 10.0.0.5</c>.
/// </summary>
public static class TrustedProxies
{
    /// <summary>
    /// Each address or network in <paramref name="value"/> as a network, a single address being a network of one.
    /// None when it's empty.
    /// </summary>
    /// <exception cref="FormatException">An entry isn't an address or a network, which would otherwise be dropped
    /// without a word, leaving the proxy untrusted.</exception>
    public static IReadOnlyList<IPNetwork> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        List<IPNetwork> networks = [];
        foreach (string entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            networks.Add(ParseEntry(entry));
        }

        return networks;
    }

    private static IPNetwork ParseEntry(string entry)
    {
        if (IPNetwork.TryParse(entry, out IPNetwork network))
        {
            return network;
        }

        if (IPAddress.TryParse(entry, out IPAddress? address))
        {
            int bits = address.GetAddressBytes().Length * 8;
            return new IPNetwork(address, bits);
        }

        throw new FormatException(
            $"'{entry}' in the trusted proxies isn't an address or a network, e.g. 10.0.0.5 or 172.18.0.0/16.");
    }
}

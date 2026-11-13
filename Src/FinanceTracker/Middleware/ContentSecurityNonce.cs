using System.Security.Cryptography;

namespace FinanceTracker.Middleware;

/// <summary>
/// A random value for one response, which its content security policy lets inline scripts carrying it run with. The
/// app's own, the start up script and the import map, carry it, an injected one can't know it. Hex, so it reads the
/// same in the header and, without encoding, in the page.
/// </summary>
public sealed class ContentSecurityNonce
{
    public string Value { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
}

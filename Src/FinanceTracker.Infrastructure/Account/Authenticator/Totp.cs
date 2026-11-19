using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FinanceTracker.Infrastructure.Account.Authenticator;

/// <summary>
/// RFC 6238 time-based one-time codes with HMAC-SHA256, as authenticator apps make them. The hashing, comparing and
/// randomness are .NET's own; this is only the RFC's counting and truncation.
/// </summary>
public static class Totp
{
    /// <summary>
    /// The step <paramref name="time"/> falls in.
    /// </summary>
    /// <param name="time">The time.</param>
    public static long StepAt(DateTimeOffset time) => time.ToUnixTimeSeconds() / AuthenticatorSettings.StepSeconds;

    /// <summary>
    /// The code for <paramref name="key"/> in <paramref name="step"/>.
    /// </summary>
    /// <param name="key">The shared key.</param>
    /// <param name="step">The time step, see <see cref="StepAt"/>.</param>
    /// <param name="digits">How many digits, 6 to 8. Other than the RFC's test vectors, always the settings'.</param>
    public static string Code(ReadOnlySpan<byte> key, long step, int digits = AuthenticatorSettings.Digits)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(digits, 6);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digits, 8);

        Span<byte> counter = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        Span<byte> hash = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(key, counter, hash);

        // RFC 4226's dynamic truncation, 31 bits from where the last nibble points
        int offset = hash[^1] & 0x0f;
        int truncated = BinaryPrimitives.ReadInt32BigEndian(hash[offset..]) & 0x7fffffff;
        int modulus = (int)Math.Pow(10, digits);
        return (truncated % modulus).ToString("D" + digits, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The step within <see cref="AuthenticatorSettings.AllowedDriftSteps"/> of <paramref name="currentStep"/> whose
    /// code is <paramref name="code"/>, or null when none is. Every step is checked and compared in fixed time, so how
    /// long it takes says nothing about the code.
    /// </summary>
    /// <param name="key">The shared key.</param>
    /// <param name="code">The code typed, digits only.</param>
    /// <param name="currentStep">The step now, see <see cref="StepAt"/>.</param>
    public static long? MatchingStep(ReadOnlySpan<byte> key, string code, long currentStep)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (code.Length != AuthenticatorSettings.Digits || !code.All(char.IsAsciiDigit))
        {
            return null;
        }

        byte[] typed = Encoding.ASCII.GetBytes(code);
        long? matched = null;
        for (long step = currentStep - AuthenticatorSettings.AllowedDriftSteps;
             step <= currentStep + AuthenticatorSettings.AllowedDriftSteps;
             step++)
        {
            byte[] expected = Encoding.ASCII.GetBytes(Code(key, step));
            if (CryptographicOperations.FixedTimeEquals(expected, typed))
            {
                matched = step;
            }
        }

        return matched;
    }
}

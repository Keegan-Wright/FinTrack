using System.Text;

namespace FinanceTracker.Infrastructure.Account.Authenticator;

/// <summary>
/// RFC 4648 base32, the form authenticator apps take keys in. Written without padding, read with or without it.
/// </summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>
    /// <paramref name="bytes"/> as upper case base32 without padding.
    /// </summary>
    /// <param name="bytes">The bytes to write.</param>
    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        StringBuilder encoded = new((bytes.Length * 8 + 4) / 5);
        int buffer = 0;
        int bits = 0;
        foreach (byte value in bytes)
        {
            // At most 4 bits are left over, so 12 bits holds them and the new byte
            buffer = ((buffer << 8) | value) & 0xfff;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                encoded.Append(Alphabet[(buffer >> bits) & 0x1f]);
            }
        }

        if (bits > 0)
        {
            encoded.Append(Alphabet[(buffer << (5 - bits)) & 0x1f]);
        }

        return encoded.ToString();
    }

    /// <summary>
    /// The bytes of <paramref name="text"/>, ignoring case, spaces and padding, as people type keys.
    /// </summary>
    /// <param name="text">The base32 text.</param>
    /// <exception cref="FormatException">It has a character base32 doesn't use.</exception>
    public static byte[] Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<byte> bytes = new(text.Length * 5 / 8);
        int buffer = 0;
        int bits = 0;
        foreach (char character in text)
        {
            if (character is ' ' or '=')
            {
                continue;
            }

            int value = Alphabet.IndexOf(char.ToUpperInvariant(character), StringComparison.Ordinal);
            if (value < 0)
            {
                throw new FormatException("The text isn't base32");
            }

            // At most 7 bits are left over, so 12 bits holds them and the new character
            buffer = ((buffer << 5) | value) & 0xfff;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)(buffer >> bits));
            }
        }

        return [.. bytes];
    }
}

using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace FinanceTracker.Infrastructure.Account.Tokens;

/// <summary>
/// Personal access tokens as <c>ftk_{lookup id}_{secret}</c>. The prefix lets secret scanners, GitHub's included,
/// recognise a leaked one. The lookup id is 8 random bytes in hex, the secret 32 random bytes in base64url. Only a
/// SHA-256 hash of the secret is kept. A slow hash isn't needed, as 256 random bits can't be guessed.
/// </summary>
public static class PersonalAccessTokenFormat
{
    /// <summary>
    /// What every token starts with.
    /// </summary>
    public const string Prefix = "ftk_";

    /// <summary>
    /// The lookup id's length in random bytes.
    /// </summary>
    private const int LookupIdBytes = 8;

    /// <summary>
    /// The secret's length in random bytes.
    /// </summary>
    private const int SecretBytes = 32;

    /// <summary>
    /// The lookup id's length in characters, two hex digits a byte.
    /// </summary>
    private const int LookupIdLength = LookupIdBytes * 2;

    /// <summary>
    /// A new random token.
    /// </summary>
    public static IssuedToken Issue()
    {
        string lookupId = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(LookupIdBytes));
        byte[] secret = RandomNumberGenerator.GetBytes(SecretBytes);
        try
        {
            string token = $"{Prefix}{lookupId}_{Base64Url.EncodeToString(secret)}";
            return new IssuedToken(token, lookupId, SHA256.HashData(secret));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    /// <summary>
    /// Reads <paramref name="token"/>'s lookup id and the hash of its secret.
    /// </summary>
    /// <param name="token">The token as given.</param>
    /// <param name="lookupId">Its public part.</param>
    /// <param name="secretHash">The SHA-256 hash of its secret part, to compare with the kept one.</param>
    /// <returns>Whether it's in the token format at all.</returns>
    public static bool TryRead(string? token, [NotNullWhen(true)] out string? lookupId,
        [NotNullWhen(true)] out byte[]? secretHash)
    {
        lookupId = null;
        secretHash = null;
        int secretStart = Prefix.Length + LookupIdLength + 1;
        if (token is null || token.Length <= secretStart || !token.StartsWith(Prefix, StringComparison.Ordinal) ||
            token[secretStart - 1] != '_')
        {
            return false;
        }

        string id = token.Substring(Prefix.Length, LookupIdLength);
        if (!id.All(char.IsAsciiHexDigitLower))
        {
            return false;
        }

        byte[] secret;
        try
        {
            secret = Base64Url.DecodeFromChars(token.AsSpan(secretStart));
        }
        catch (FormatException)
        {
            return false;
        }

        try
        {
            if (secret.Length != SecretBytes)
            {
                return false;
            }

            lookupId = id;
            secretHash = SHA256.HashData(secret);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    /// <summary>
    /// Whether <paramref name="secretHash"/> is <paramref name="keptHash"/>, compared in fixed time.
    /// </summary>
    /// <param name="secretHash">The hash of the secret given.</param>
    /// <param name="keptHash">The hash kept for the token.</param>
    public static bool Matches(byte[] secretHash, byte[] keptHash) =>
        CryptographicOperations.FixedTimeEquals(secretHash, keptHash);
}

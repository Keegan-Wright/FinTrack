using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FinanceTracker.Infrastructure;

namespace FinanceTracker.Tests.Unit.Shared;

/// <summary>
/// Fixed, throwaway keyrings for tests. Each key id always maps to the same key, so keyrings built separately (e.g.
/// before and after a rotation) agree on shared keys.
/// </summary>
public static class TestEncryptionKeys
{
    /// <summary>
    /// A keyring holding <paramref name="keyIds"/> (key 1 when none are given), writing with
    /// <paramref name="currentKeyId"/>.
    /// </summary>
    public static EncryptionConfiguration Keyring(int currentKeyId = 1, params int[] keyIds) => new()
    {
        Keys = (keyIds.Length == 0 ? [currentKeyId] : keyIds).ToDictionary(static id => id, Key),
        CurrentKeyId = currentKeyId
    };

    /// <summary>
    /// <see cref="Keyring"/> in the <c>id:base64key</c> form configuration takes.
    /// </summary>
    public static string Serialise(EncryptionConfiguration configuration) => string.Join(';',
        configuration.Keys.Select(static x =>
            string.Create(CultureInfo.InvariantCulture, $"{x.Key}:{Convert.ToBase64String(x.Value)}")));

    public static byte[] Key(int id) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"FinTrack test key {id}")));
}

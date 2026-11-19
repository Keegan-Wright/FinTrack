using System.Text;
using FinanceTracker.Infrastructure.Account.Authenticator;
using QRCoder;

namespace FinanceTracker.Components.Account;

/// <summary>
/// The authenticator key as a QR code that authenticator apps scan, drawn on the server as SVG.
/// </summary>
internal static class AuthenticatorQrCode
{
    /// <summary>
    /// The QR code for <paramref name="key"/>, labelled with <paramref name="username"/>.
    /// </summary>
    /// <param name="username">Who the key is for, shown in the app.</param>
    /// <param name="key">The unformatted authenticator key.</param>
    public static string Svg(string username, string key)
    {
        using QRCodeGenerator generator = new();
        using QRCodeData data = generator.CreateQrCode(AuthenticatorUri.For(username, key), QRCodeGenerator.ECCLevel.Q);
        using SvgQRCode code = new(data);

        return code.GetGraphic(4, "#000000", "#ffffff", drawQuietZones: true);
    }

    /// <summary>
    /// <paramref name="key"/> in lower case groups of four, for typing into the app.
    /// </summary>
    /// <param name="key">The unformatted authenticator key.</param>
    public static string Grouped(string key)
    {
        StringBuilder grouped = new();
        for (int start = 0; start < key.Length; start += 4)
        {
            if (grouped.Length > 0)
            {
                grouped.Append(' ');
            }

            grouped.Append(key.AsSpan(start, Math.Min(4, key.Length - start)));
        }

        return grouped.ToString().ToLowerInvariant();
    }
}

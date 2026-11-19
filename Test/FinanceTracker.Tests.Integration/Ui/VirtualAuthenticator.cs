using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Chromium's virtual authenticator, through the DevTools protocol, standing in for a phone or laptop that makes and
/// uses passkeys. It answers as if the person gave their fingerprint, so tests needn't.
/// </summary>
public static class VirtualAuthenticator
{
    /// <summary>
    /// Adds a platform authenticator with user verification to <paramref name="page"/>, as a laptop's fingerprint
    /// reader or a phone's would be. Passkeys it makes stay with it for the page's life.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="offerInUsernameField">Whether the sign in page offers passkeys in the username field. The
    /// virtual authenticator answers that offer by itself as the page opens, where a person would pick one, so tests
    /// of the button turn it off.</param>
    public static async Task AddToAsync(IPage page, bool offerInUsernameField = false)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!offerInUsernameField)
        {
            await page.AddInitScriptAsync(
                "PublicKeyCredential.isConditionalMediationAvailable = async () => false;");
        }

        ICDPSession session = await page.Context.NewCDPSessionAsync(page);
        await session.SendAsync("WebAuthn.enable");
        await session.SendAsync("WebAuthn.addVirtualAuthenticator", new Dictionary<string, object>
        {
            ["options"] = new Dictionary<string, object>
            {
                ["protocol"] = "ctap2",
                ["transport"] = "internal",
                ["hasResidentKey"] = true,
                ["hasUserVerification"] = true,
                ["isUserVerified"] = true,
                ["automaticPresenceSimulation"] = true
            }
        });
    }
}

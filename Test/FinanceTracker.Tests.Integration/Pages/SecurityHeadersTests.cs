using System.Text.RegularExpressions;
using FinanceTracker.Tests.Integration.Http;

namespace FinanceTracker.Tests.Integration.Pages;

/// <summary>
/// The browser security headers on the app's pages, and the page's own inline scripts allowed by them.
/// </summary>
public partial class SecurityHeadersTests : IntegrationTestBase
{
    [Test]
    public async Task Page_HasTheSecurityHeaders()
    {
        // Arrange
        using FinTrackClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/Account/Login", CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Header(response, "X-Content-Type-Options")).IsEqualTo("nosniff");
            await Assert.That(Header(response, "X-Frame-Options")).IsEqualTo("DENY");
            await Assert.That(Header(response, "Referrer-Policy")).IsEqualTo("strict-origin-when-cross-origin");
            await Assert.That(Header(response, "Content-Security-Policy")).Contains("frame-ancestors 'none'");
        }
    }

    [Test]
    public async Task Page_InlineScriptsCarryThePolicysNonce_NewEachTime()
    {
        // Arrange
        using FinTrackClient client = CreateClient();

        // Act
        HttpResponseMessage first = await client.GetAsync("/Account/Login", CancellationToken);
        HttpResponseMessage second = await client.GetAsync("/Account/Login", CancellationToken);
        string policy = Header(first, "Content-Security-Policy");
        string page = await first.Content.ReadAsStringAsync(CancellationToken);

        // Assert, the start up script and the import map
        string nonce = PolicyNonce().Match(policy).Groups[1].Value;
        using (Assert.Multiple())
        {
            await Assert.That(nonce).IsNotEmpty();
            await Assert.That(PageNonce().Matches(page).Select(static x => x.Groups[1].Value))
                .IsEquivalentTo([nonce, nonce]);
            await Assert.That(Header(second, "Content-Security-Policy")).DoesNotContain(nonce);
        }
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out IEnumerable<string>? values) ? string.Join(",", values) : string.Empty;

    [GeneratedRegex("'nonce-([^']+)'")]
    private static partial Regex PolicyNonce();

    [GeneratedRegex("<script[^>]*nonce=\"([^\"]+)\"")]
    private static partial Regex PageNonce();
}

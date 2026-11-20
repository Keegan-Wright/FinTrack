using System.Buffers.Binary;
using System.Text.Json;
using System.Text.RegularExpressions;
using FinanceTracker.Design;
using FinanceTracker.Tests.Integration.Http;

namespace FinanceTracker.Tests.Integration.Pages;

/// <summary>
/// The web app manifest that lets FinTrack be installed, and the page head that points to it.
/// </summary>
public partial class WebAppManifestTests : IntegrationTestBase
{
    [Test]
    public async Task Manifest_SignedOut_IsServedWithTheDarkPalettesColours()
    {
        // Arrange, signed out, as browsers fetch the manifest without cookies
        using FinTrackClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/site.webmanifest", CancellationToken);
        using JsonDocument manifest = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        JsonElement root = manifest.RootElement;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.IsSuccessStatusCode).IsTrue();
            await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/manifest+json");
            await Assert.That(root.GetProperty("name").GetString()).IsEqualTo("FinTrack");
            await Assert.That(root.GetProperty("short_name").GetString()).IsEqualTo("FinTrack");
            await Assert.That(root.GetProperty("start_url").GetString()).IsEqualTo("/");
            await Assert.That(root.GetProperty("display").GetString()).IsEqualTo("standalone");
            await Assert.That(root.GetProperty("theme_color").GetString()).IsEqualTo(DarkPalette.AppbarBackground);
            await Assert.That(root.GetProperty("background_color").GetString()).IsEqualTo(DarkPalette.Background);
        }
    }

    [Test]
    public async Task Manifest_Icons_AreServedAtTheirSizes()
    {
        // Arrange
        using FinTrackClient client = CreateClient();
        HttpResponseMessage response = await client.GetAsync("/site.webmanifest", CancellationToken);
        using JsonDocument manifest = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken));
        JsonElement[] icons = [.. manifest.RootElement.GetProperty("icons").EnumerateArray()];

        // Act and assert, each one is a PNG of the size it says, and Android has a maskable one
        await Assert.That(icons.Select(static x => x.GetProperty("purpose").GetString())).Contains("maskable");
        foreach (JsonElement icon in icons)
        {
            HttpResponseMessage image = await client.GetAsync(icon.GetProperty("src").GetString()!, CancellationToken);
            byte[] bytes = await image.Content.ReadAsByteArrayAsync(CancellationToken);
            using (Assert.Multiple())
            {
                await Assert.That(image.Content.Headers.ContentType?.MediaType).IsEqualTo("image/png");
                await Assert.That(PngSize(bytes)).IsEqualTo(icon.GetProperty("sizes").GetString());
            }
        }
    }

    [Test]
    public async Task SignInPage_LinksTheManifest_AndHasTheDarkThemeColour()
    {
        // Arrange
        using FinTrackClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/Account/Login", CancellationToken);
        string page = await response.Content.ReadAsStringAsync(CancellationToken);

        // Assert, the sign in pages are always dark
        using (Assert.Multiple())
        {
            await Assert.That(page).Contains("<link rel=\"manifest\" href=\"site.webmanifest\"");
            await Assert.That(ThemeColours().Matches(page).Select(static x => x.Groups[1].Value))
                .IsEquivalentTo([DarkPalette.AppbarBackground]);
            await Assert.That(page).Contains("viewport-fit=cover");
        }
    }

    // A PNG's width and height, which sit at a fixed place in its header, e.g. 512x512
    private static string PngSize(byte[] png)
    {
        int width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));
        int height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4));
        return $"{width}x{height}";
    }

    [GeneratedRegex("<meta name=\"theme-color\" content=\"([^\"]+)\"")]
    private static partial Regex ThemeColours();
}

using System.Net;
using System.Net.Http.Headers;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Tests.Integration.Http;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Making an API token on its page, reading with it as a script would, and revoking it.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class ApiTokensFlowTests : UiTestBase
{
    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task MakeReadAndRevoke_FromTheApiTokensPage(ScreenSize screen)
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(screen, user);
        await GoToAsync(page, "/ApiTokens");

        // Act, make one
        await Button(page, "New token").ClickAsync();
        ILocator dialog = Dialog(page);
        await dialog.GetByLabel("Name").FillAsync("Placeholder script");
        await dialog.GetByLabel("Your password").FillAsync(SignedInUser.DefaultPassword);
        List<string> dialogProblems = await AccessibilityProblemsAsync(page);
        await Button(page, "OK").ClickAsync();
        ILocator tokenField = Dialog(page).GetByLabel("Token");
        await Assertions.Expect(tokenField).ToHaveValueAsync(new System.Text.RegularExpressions.Regex("^ftk_"));
        string token = await tokenField.InputValueAsync();
        List<string> createdTooWide = await WhatScrollsSidewaysAsync(page);
        await Button(page, "Done").ClickAsync();
        await Assertions.Expect(page.Locator(".ft-tokens__row")).ToHaveCountAsync(1);
        List<string> pageProblems = await AccessibilityProblemsAsync(page);
        List<string> pageTooWide = await WhatScrollsSidewaysAsync(page);
        HttpStatusCode before = await ReadWithAsync(token);

        // Then revoke it
        await Button(page, "Revoke Placeholder script").ClickAsync();
        await Dialog(page).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Revoke", Exact = true })
            .ClickAsync();
        await Assertions.Expect(page.Locator(".ft-tokens__row")).ToHaveCountAsync(0);

        // Assert
        HttpStatusCode after = await ReadWithAsync(token);
        using (Assert.Multiple())
        {
            await Assert.That(before).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(after).IsEqualTo(HttpStatusCode.Unauthorized);
            await Assert.That(dialogProblems).IsEmpty();
            await Assert.That(pageProblems).IsEmpty();
            await Assert.That(createdTooWide).IsEmpty();
            await Assert.That(pageTooWide).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    /// <summary>
    /// Reads the user's calendar with only <paramref name="token"/>, as a script would.
    /// </summary>
    private async Task<HttpStatusCode> ReadWithAsync(string token)
    {
        using FinTrackClient client = CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, $"{ApiRoutes.Calendar}/events");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response =
            await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, CancellationToken);
        return response.StatusCode;
    }
}

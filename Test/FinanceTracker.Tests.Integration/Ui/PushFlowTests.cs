using System.Net;
using System.Text.Json;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Playwright;

namespace FinanceTracker.Tests.Integration.Ui;

/// <summary>
/// Notification settings' Push tab as someone turning push on uses it. The test browser has no real push service, so
/// subscribing hands back a stand in browser's keys and a push service on this machine, then the app's own test push
/// goes there and is read with those keys.
/// </summary>
[ParallelLimiter<UiParallelLimit>]
public class PushFlowTests : UiTestBase
{
    [Test]
    [MethodDataSource(typeof(ScreenSize), nameof(ScreenSize.All))]
    public async Task TurnOn_ThenSendATest_ReachesThisDevice(ScreenSize screen)
    {
        // Arrange
        using WebhookCatcher pushService = new(HttpStatusCode.Created);
        using WebPushTestBrowser browser = new();
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(screen, user);
        // Headless Chromium says notifications are blocked unless they're granted
        await page.Context.GrantPermissionsAsync(["notifications"]);
        await OpenPushTabAsync(page);
        await page.EvaluateAsync("subscription => { window.ftPushSubscribe = async () => subscription; }", new
        {
            endpoint = pushService.Url.ToString(),
            publicKey = browser.PublicKeyText,
            authSecret = browser.AuthSecretText,
            name = "Chrome on Android"
        });

        // Act
        await Button(page, "Turn on for this device").ClickAsync();
        await Assertions.Expect(Button(page, "Turn off for this device")).ToBeVisibleAsync();
        await Button(page, "Send a test").ClickAsync();

        // Assert
        CaughtPost post = await pushService.NextAsync(CancellationToken);
        using JsonDocument message = JsonDocument.Parse(browser.Decrypt(post.Body));
        await Assertions.Expect(page.GetByText("Last push sent")).ToBeVisibleAsync();
        using (Assert.Multiple())
        {
            await Assert.That(message.RootElement.GetProperty("title").GetString()).IsEqualTo("FinTrack");
            await Assert.That(await page.Locator(".ft-push__row").InnerTextAsync()).Contains("This device");
            await Assert.That(await WhatScrollsSidewaysAsync(page)).IsEmpty();
            await Assert.That(await AccessibilityProblemsAsync(page)).IsEmpty();
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    [Test]
    public async Task ServiceWorker_Registered_Activates()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPage page = await OpenAsync(ScreenSize.Desktop, user);
        await GoToAsync(page, "/");
        await WaitUntilInteractiveAsync(page);

        // Act, as turning push on registers it
        string state = await page.EvaluateAsync<string>("""
            async () => {
                await navigator.serviceWorker.register("service-worker.js", {scope: "./"});
                const worker = (await navigator.serviceWorker.ready).active;
                if (worker.state !== "activated") {
                    await new Promise(resolve => worker.addEventListener("statechange",
                        () => worker.state === "activated" && resolve()));
                }
                return worker.state;
            }
            """);

        // Assert, the page carries on loading from the server, the worker has nothing to serve it from
        await GoToAsync(page, "/Notifications/Settings");
        using (Assert.Multiple())
        {
            await Assert.That(state).IsEqualTo("activated");
            await Assert.That(BrowserErrors).IsEmpty();
        }
    }

    private static async Task OpenPushTabAsync(IPage page)
    {
        await GoToAsync(page, "/Notifications/Settings");
        await WaitUntilLoadedAsync(page);
        await page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = "Push", Exact = true }).ClickAsync();
        await Assertions.Expect(Button(page, "Turn on for this device")).ToBeVisibleAsync();
    }
}

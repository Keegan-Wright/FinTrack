using System.Net;
using Bunit.TestDoubles;
using FinanceTracker.Client.Api;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Unit.Client.Api;

public class SignedOutHandlerTests : ComponentTestBase
{
    private BunitNavigationManager Navigation =>
        (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

    [Test]
    public async Task Send_ServerSaysSignedOut_GoesToSignInAndBackHereAfterWithoutFinishing()
    {
        // Arrange
        Navigation.NavigateTo("Bills?month=2026-11");
        using HttpClient client = ClientAnswering(HttpStatusCode.Unauthorized);
        using CancellationTokenSource leaving = new();

        // Act
        Task<HttpResponseMessage> sending = client.GetAsync(new Uri("api/bills", UriKind.Relative), leaving.Token);
        using (CancellationTokenSource timeout = new(TestTimeouts.Wait))
        {
            while (Navigation.History.Count < 2)
            {
                await Task.Delay(10, timeout.Token);
            }
        }

        // Assert, on its way to sign in, the call left waiting until the page goes
        NavigationHistory last = Navigation.History.First();
        using (Assert.Multiple())
        {
            await Assert.That(last.Uri)
                .IsEqualTo($"Account/Login?returnUrl={Uri.EscapeDataString("http://localhost/Bills?month=2026-11")}");
            await Assert.That(last.Options.ForceLoad).IsTrue();
            await Assert.That(sending.IsCompleted).IsFalse();
        }

        await leaving.CancelAsync();
        await Assert.ThrowsAsync<TaskCanceledException>(() => sending);
    }

    [Test]
    [Arguments(HttpStatusCode.OK)]
    [Arguments(HttpStatusCode.Forbidden)]
    [Arguments(HttpStatusCode.InternalServerError)]
    public async Task Send_AnyOtherAnswer_StaysOnThePage(HttpStatusCode status)
    {
        // Arrange
        Navigation.NavigateTo("Bills");
        using HttpClient client = ClientAnswering(status);

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri("api/bills", UriKind.Relative));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(status);
            await Assert.That(Navigation.Uri).IsEqualTo("http://localhost/Bills");
        }
    }

    private HttpClient ClientAnswering(HttpStatusCode status) =>
        new(new SignedOutHandler(Navigation) { InnerHandler = new AnsweringHandler(status) })
        {
            BaseAddress = new Uri("http://localhost/")
        };
}

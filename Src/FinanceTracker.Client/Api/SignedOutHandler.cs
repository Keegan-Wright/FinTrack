using System.Net;
using FinanceTracker.Client.Extensions;
using Microsoft.AspNetCore.Components;

namespace FinanceTracker.Client.Api;

/// <summary>
/// Sends the user to sign in when the server says they no longer are, e.g. they signed out in another tab or their
/// session ran out. The app in the browser still thinks they're signed in, so without this each page would only say
/// it couldn't load. The call doesn't finish, the browser is leaving, so the page doesn't flash an error on the way.
/// </summary>
public sealed class SignedOutHandler : DelegatingHandler
{
    private readonly NavigationManager _navigation;

    public SignedOutHandler(NavigationManager navigation) => _navigation = navigation;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        _navigation.NavigateToSignIn();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new OperationCanceledException(cancellationToken);
    }
}

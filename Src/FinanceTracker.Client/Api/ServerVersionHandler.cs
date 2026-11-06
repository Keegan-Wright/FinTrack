using FinanceTracker.Contracts.Api;

namespace FinanceTracker.Client.Api;

/// <summary>
/// Reads the server's build from each API answer, see <see cref="ApiHeaders.Version"/>, so <see cref="AppUpdate"/>
/// knows when FinTrack has been updated under this tab.
/// </summary>
public sealed class ServerVersionHandler : DelegatingHandler
{
    private readonly AppUpdate _update;

    public ServerVersionHandler(AppUpdate update) => _update = update;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues(ApiHeaders.Version, out IEnumerable<string>? versions))
        {
            _update.ServerRuns(versions.FirstOrDefault());
        }

        return response;
    }
}

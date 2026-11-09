using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Integration;

/// <summary>
/// A webhook on this machine for the app to post to, keeping each post as whatever receives it would see it.
/// </summary>
public sealed class WebhookCatcher : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Channel<CaughtPost> _posts = Channel.CreateUnbounded<CaughtPost>();

    public WebhookCatcher()
    {
        Url = new Uri($"http://127.0.0.1:{FreePort()}/hook/");
        _listener.Prefixes.Add(Url.ToString());
        _listener.Start();
        _ = ListenAsync();
    }

    /// <summary>
    /// Where to post to it.
    /// </summary>
    public Uri Url { get; }

    /// <summary>
    /// The next post, waiting for it to arrive.
    /// </summary>
    public async Task<CaughtPost> NextAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TestTimeouts.Job);
        return await _posts.Reader.ReadAsync(timeout.Token);
    }

    public void Dispose() => _listener.Close();

    private async Task ListenAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            using MemoryStream body = new();
            await context.Request.InputStream.CopyToAsync(body);
            Dictionary<string, string> headers = context.Request.Headers.AllKeys
                .Where(static x => x is not null)
                .ToDictionary(static x => x!, x => context.Request.Headers[x]!, StringComparer.OrdinalIgnoreCase);

            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            context.Response.Close();
            await _posts.Writer.WriteAsync(new CaughtPost(headers, body.ToArray()));
        }
    }

    private static int FreePort()
    {
        using TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }
}

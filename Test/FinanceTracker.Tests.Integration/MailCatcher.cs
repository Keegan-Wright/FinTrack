using System.Net.Http.Json;
using System.Text.Json;
using FinanceTracker.Tests.Unit.Shared;

namespace FinanceTracker.Tests.Integration;

/// <summary>
/// The Mailpit the AppHost runs while developing and testing, read through its API, so tests see the emails the app
/// sent as someone's inbox would.
/// </summary>
public sealed class MailCatcher : IDisposable
{
    private readonly HttpClient _http;

    public MailCatcher(Uri baseAddress) => _http = new HttpClient { BaseAddress = baseAddress };

    /// <summary>
    /// The emails sent to <paramref name="to"/> once there's at least one, newest first.
    /// </summary>
    /// <exception cref="TimeoutException">None arrived within <see cref="TestTimeouts.Job"/>.</exception>
    public async Task<IReadOnlyList<CaughtMail>> WaitForAsync(string to, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TestTimeouts.Job);
        try
        {
            while (true)
            {
                List<string> ids = await FindAsync(to, timeout.Token);
                if (ids.Count > 0)
                {
                    List<CaughtMail> mail = [];
                    foreach (string id in ids)
                    {
                        mail.Add(await ReadAsync(id, timeout.Token));
                    }

                    return mail;
                }

                await Task.Delay(250, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"No email to {to} arrived");
        }
    }

    public void Dispose() => _http.Dispose();

    private async Task<List<string>> FindAsync(string to, CancellationToken cancellationToken)
    {
        using JsonDocument found = await _http.GetFromJsonAsync<JsonDocument>(
            $"api/v1/search?query={Uri.EscapeDataString($"to:\"{to}\"")}", cancellationToken)
            ?? throw new InvalidOperationException("Mailpit gave no answer");

        return found.RootElement.GetProperty("messages").EnumerateArray()
            .Select(static x => x.GetProperty("ID").GetString()!)
            .ToList();
    }

    private async Task<CaughtMail> ReadAsync(string id, CancellationToken cancellationToken)
    {
        using JsonDocument message = await _http.GetFromJsonAsync<JsonDocument>($"api/v1/message/{id}",
            cancellationToken) ?? throw new InvalidOperationException("Mailpit gave no answer");

        JsonElement root = message.RootElement;
        return new CaughtMail(root.GetProperty("Subject").GetString()!, root.GetProperty("Text").GetString()!,
            root.GetProperty("HTML").GetString()!);
    }
}

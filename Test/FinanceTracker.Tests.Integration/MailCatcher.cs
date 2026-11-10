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
    /// The emails sent to <paramref name="to"/> with <paramref name="subject"/> once there's at least one, newest
    /// first. By subject too, as registering sends a confirmation email to every new user.
    /// </summary>
    /// <exception cref="TimeoutException">None arrived within <see cref="TestTimeouts.Job"/>.</exception>
    public async Task<IReadOnlyList<CaughtMail>> WaitForAsync(string to, string subject,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TestTimeouts.Job);
        try
        {
            while (true)
            {
                List<string> ids = await FindAsync(to, subject, timeout.Token);
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
            throw new TimeoutException($"No email to {to} about {subject} arrived");
        }
    }

    /// <summary>
    /// Whether an email with <paramref name="subject"/> has been sent to <paramref name="to"/> yet, without waiting.
    /// Mailpit has an email by the time the app's send returns, so once a page that sends has answered, none here
    /// means none was sent.
    /// </summary>
    public async Task<bool> HasAnyAsync(string to, string subject, CancellationToken cancellationToken) =>
        (await FindAsync(to, subject, cancellationToken)).Count > 0;

    public void Dispose() => _http.Dispose();

    private async Task<List<string>> FindAsync(string to, string subject, CancellationToken cancellationToken)
    {
        using JsonDocument found = await _http.GetFromJsonAsync<JsonDocument>(
            $"api/v1/search?query={Uri.EscapeDataString($"to:\"{to}\"")}", cancellationToken)
            ?? throw new InvalidOperationException("Mailpit gave no answer");

        // Matched here rather than by Mailpit's search, which matches part of a subject
        return found.RootElement.GetProperty("messages").EnumerateArray()
            .Where(x => x.GetProperty("Subject").GetString() == subject)
            .Select(static x => x.GetProperty("ID").GetString()!)
            .ToList();
    }

    private async Task<CaughtMail> ReadAsync(string id, CancellationToken cancellationToken)
    {
        using JsonDocument message = await _http.GetFromJsonAsync<JsonDocument>($"api/v1/message/{id}",
            cancellationToken) ?? throw new InvalidOperationException("Mailpit gave no answer");

        JsonElement root = message.RootElement;
        List<string> inline = root.GetProperty("Inline").EnumerateArray()
            .Select(static x => x.GetProperty("ContentID").GetString()!)
            .ToList();
        return new CaughtMail(root.GetProperty("Subject").GetString()!, root.GetProperty("Text").GetString()!,
            root.GetProperty("HTML").GetString()!, inline);
    }
}

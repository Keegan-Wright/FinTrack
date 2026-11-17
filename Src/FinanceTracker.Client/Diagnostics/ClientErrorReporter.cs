using FinanceTracker.Contracts.Diagnostics;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceTracker.Client.Diagnostics;

/// <summary>
/// Sends the errors <see cref="ClientErrorLogger"/> hears to the server, see <see cref="IClientErrorsService"/>. In
/// the background, so the page carries on, and a failure to send is dropped rather than logged, which would report
/// itself. The same error is sent once, and at most <see cref="MaxReports"/> per app load, so a page failing in a loop
/// can't flood the server.
/// </summary>
public sealed class ClientErrorReporter
{
    /// <summary>
    /// The most errors one load of the app sends.
    /// </summary>
    public const int MaxReports = 20;

    private readonly IServiceProvider _serviceProvider;
    private readonly HashSet<string> _sent = [];
    private readonly List<Task> _sending = [];
    private readonly Lock _lock = new();

    // Set while a report is being sent, so anything logged by sending it isn't reported in turn
    private static readonly AsyncLocal<bool> Reporting = new();

    /// <param name="serviceProvider">Gives the API client, resolved when a report is sent.</param>
    public ClientErrorReporter(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    /// <summary>
    /// Sends the error unless it's been sent already, the limit's reached, or it was logged by sending a report.
    /// </summary>
    /// <param name="category">The logger's category.</param>
    /// <param name="message">What was logged.</param>
    /// <param name="exception">The exception, null for none.</param>
    public void Report(string category, string message, Exception? exception)
    {
        if (Reporting.Value)
        {
            return;
        }

        ClientError error = Build(category, message, exception);
        lock (_lock)
        {
            if (_sent.Count >= MaxReports || !_sent.Add(SameErrorKey(error)))
            {
                return;
            }

            _sending.Add(SendAsync(error));
        }
    }

    /// <summary>
    /// Waits for the reports being sent, for tests.
    /// </summary>
    internal Task WhenSentAsync()
    {
        lock (_lock)
        {
            return Task.WhenAll(_sending);
        }
    }

    /// <summary>
    /// Sends the error to the server. A failure is dropped, as logging it would report it again.
    /// </summary>
    /// <param name="error">The error.</param>
    private async Task SendAsync(ClientError error)
    {
        Reporting.Value = true;
        try
        {
            await using AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IClientErrorsService>()
                .ReportAsync(error, CancellationToken.None);
        }
        catch (Exception)
        {
            // Dropped, logging it would report it, and the page has its own error to show
        }
    }

    /// <summary>
    /// The error as it's sent, each part cut to its limit.
    /// </summary>
    /// <param name="category">The logger's category.</param>
    /// <param name="message">What was logged.</param>
    /// <param name="exception">The exception, null for none.</param>
    private ClientError Build(string category, string message, Exception? exception) => new()
    {
        Message = Cut(exception?.Message ?? message, ClientError.MaxMessageLength),
        ExceptionType = exception is null ? null : Cut(exception.GetType().FullName ?? "", ClientError.MaxNameLength),
        StackTrace = exception?.ToString() is { } trace ? Cut(trace, ClientError.MaxStackTraceLength) : null,
        Category = Cut(category, ClientError.MaxNameLength),
        Page = Cut(CurrentPage(), ClientError.MaxNameLength)
    };

    // Relative to the app and without the query, which can carry ids and searches
    /// <summary>
    /// The page's path, without its query.
    /// </summary>
    private string CurrentPage()
    {
        NavigationManager navigation = _serviceProvider.GetRequiredService<NavigationManager>();
        string relative = navigation.ToBaseRelativePath(navigation.Uri);
        int queryAt = relative.IndexOfAny(['?', '#']);
        if (queryAt >= 0)
        {
            relative = relative[..queryAt];
        }

        return "/" + relative;
    }

    // The same error on the same page is sent once
    /// <summary>
    /// What makes two errors the same one, so it's sent once.
    /// </summary>
    /// <param name="error">The error.</param>
    private static string SameErrorKey(ClientError error) =>
        $"{error.Page}|{error.Category}|{error.ExceptionType}|{error.Message}";

    /// <summary>
    /// <paramref name="value"/> cut to <paramref name="length"/>.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <param name="length">The most it may be.</param>
    private static string Cut(string value, int length)
    {
        if (value.Length <= length)
        {
            return value;
        }

        return value[..length];
    }
}

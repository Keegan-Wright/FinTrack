using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Client.Diagnostics;

/// <summary>
/// Gives every logger in the browser a <see cref="ClientErrorLogger"/>, so errors reach the server's logs. The
/// reporter is resolved on the first error, as logging is set up before the services it sends through.
/// </summary>
public sealed class ClientErrorLoggerProvider : ILoggerProvider
{
    private readonly Lazy<ClientErrorReporter> _reporter;

    public ClientErrorLoggerProvider(IServiceProvider services) =>
        _reporter = new Lazy<ClientErrorReporter>(services.GetRequiredService<ClientErrorReporter>);

    public ILogger CreateLogger(string categoryName) => new ClientErrorLogger(categoryName, () => _reporter.Value);

    public void Dispose()
    {
    }
}

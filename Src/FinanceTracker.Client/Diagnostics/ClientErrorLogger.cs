namespace FinanceTracker.Client.Diagnostics;

/// <summary>
/// Hands what's logged at Error or Critical to <see cref="ClientErrorReporter"/> Blazor logs a page's unhandled
/// exception here, whether the layout's error boundary caught it or not. The browser's console still gets it too.
/// </summary>
public sealed class ClientErrorLogger : ILogger
{
    private readonly string _category;
    private readonly Func<ClientErrorReporter> _reporter;

    /// <param name="category">The logger's category, sent with each error.</param>
    /// <param name="reporter">Gives the reporter, once there's an error.</param>
    public ClientErrorLogger(string category, Func<ClientErrorReporter> reporter)
    {
        _category = category;
        _reporter = reporter;
    }

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        if (!IsEnabled(logLevel))
        {
            return;
        }

        _reporter().Report(_category, formatter(state, exception), exception);
    }
}

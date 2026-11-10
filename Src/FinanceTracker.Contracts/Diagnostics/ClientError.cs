namespace FinanceTracker.Contracts.Diagnostics;

/// <summary>
/// An error the app in the browser hit, sent to the server so it's in the server's logs with everything else. The
/// browser cuts each part to its limit before sending.
/// </summary>
public sealed class ClientError
{
    /// <summary>
    /// The most <see cref="Message"/> can hold.
    /// </summary>
    public const int MaxMessageLength = 2000;

    /// <summary>
    /// The most <see cref="StackTrace"/> can hold.
    /// </summary>
    public const int MaxStackTraceLength = 8000;

    /// <summary>
    /// The most <see cref="Category"/>, <see cref="ExceptionType"/> and <see cref="Page"/> can each hold.
    /// </summary>
    public const int MaxNameLength = 500;

    /// <summary>
    /// What went wrong, the exception's message or what was logged.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The exception's type, null when there was none.
    /// </summary>
    public string? ExceptionType { get; init; }

    /// <summary>
    /// Where in the code it happened, null when there was no exception.
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// What logged it, e.g. Blazor's error boundary.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// The page it happened on, relative to the app and without its query, e.g. <c>/Budgets</c>.
    /// </summary>
    public required string Page { get; init; }
}

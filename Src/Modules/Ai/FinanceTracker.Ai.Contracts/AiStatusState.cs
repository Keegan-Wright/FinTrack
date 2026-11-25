namespace FinanceTracker.Ai.Contracts;

/// <summary>
/// The signed in user's AI status, fetched once and shared by the AI module's components in the app bar, the menu and
/// Your profile, so turning AI on or off shows everywhere at once. The app registers it, since the module's browser
/// assembly loads after the app has started.
/// </summary>
/// <param name="aiService">Reads and changes the status.</param>
public sealed class AiStatusState(IAiService aiService)
{
    private Task<AiStatus>? _status;

    /// <summary>
    /// Raised when the user turns AI on or off.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// The status, fetched the first time it's asked for, and again after a fetch that failed.
    /// </summary>
    public Task<AiStatus> GetAsync()
    {
        if (_status is null || _status.IsFaulted)
        {
            _status = aiService.GetStatusAsync(CancellationToken.None);
        }

        return _status;
    }

    /// <summary>
    /// Fetches the status again, then tells everything showing it, e.g. once an admin has set AI up.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    public async Task<AiStatus> RefreshAsync(CancellationToken cancellationToken)
    {
        AiStatus status = await aiService.GetStatusAsync(cancellationToken);
        _status = Task.FromResult(status);
        Changed?.Invoke();
        return status;
    }

    /// <summary>
    /// Turns AI on or off for the user, then tells everything showing it.
    /// </summary>
    /// <param name="optedIn">Whether they want it on.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    public async Task<AiStatus> ChangeOptInAsync(bool optedIn, CancellationToken cancellationToken)
    {
        AiStatus status = await aiService.ChangeOptInAsync(new ChangeAiOptIn { OptedIn = optedIn }, cancellationToken);
        _status = Task.FromResult(status);
        Changed?.Invoke();
        return status;
    }
}

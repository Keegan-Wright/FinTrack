using FinanceTracker.Client.Layout;

namespace FinanceTracker.Client;

/// <summary>
/// Whether FinTrack has been updated since this tab loaded it the server runs another build than the one running
/// here, <see cref="AppVersion"/>, so its API may have changed. The tab carries on, and reloading it, when the user
/// chooses, gets the new one. Heard from each API answer's version, see <c>ServerVersionHandler</c>.
/// </summary>
public sealed class AppUpdate
{
    private readonly string _running;

    public AppUpdate() : this(AppVersion.Full)
    {
    }

    /// <param name="running">The build running in this tab.</param>
    public AppUpdate(string running) => _running = running;

    /// <summary>
    /// Whether the server runs another build. Once it does, it stays so until the tab reloads.
    /// </summary>
    public bool Available { get; private set; }

    /// <summary>
    /// Raised once, when <see cref="Available"/> becomes true.
    /// </summary>
    public event EventHandler? AvailableChanged;

    /// <summary>
    /// The build the server says it runs, <see langword="null"/> when an answer didn't say.
    /// </summary>
    public void ServerRuns(string? version)
    {
        if (Available || string.IsNullOrEmpty(version) || string.Equals(version, _running, StringComparison.Ordinal))
        {
            return;
        }

        Available = true;
        AvailableChanged?.Invoke(this, EventArgs.Empty);
    }
}

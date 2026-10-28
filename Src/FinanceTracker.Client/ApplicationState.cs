using System.ComponentModel;
using System.Runtime.CompilerServices;
using FinanceTracker.Contracts.OpenBanking;

namespace FinanceTracker.Client;

/// <summary>
/// UI state shared by the layout and the page inside it, cascaded from the root. Pages set it through
/// <see cref="Pages.PageComponent"/>, the layout listens for changes and shows the loading overlay and sync status.
/// </summary>
/// <remarks>
/// One instance per circuit (each browser tab).
/// </remarks>
public sealed class ApplicationState : INotifyPropertyChanged
{
    /// <summary>
    /// Whether the loading overlay covers the page.
    /// </summary>
    public bool Loading
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// The text under the loading overlay's spinner.
    /// </summary>
    public string? LoadingMessage
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Whether a sync of the user's accounts has been queued and not yet finished, until the sync notification
    /// arrives.
    /// </summary>
    public bool Syncing
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// How many transactions might be transfers and are waiting for the user, shown in the navigation. Refreshed after
    /// each sync and each decision.
    /// </summary>
    public int TransfersToReview
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// How many bank connections need their consent renewing soon or already have lapsed, shown in the navigation and
    /// a banner. Refreshed after each sync or renewal.
    /// </summary>
    public int ConnectionsToRenew
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Sets <see cref="ConnectionsToRenew"/> from the user's connections: those not <see cref="ConsentState.Active"/>.
    /// </summary>
    public void CountConnectionsToRenew(IEnumerable<BankConnectionSummary> connections) =>
        ConnectionsToRenew = connections.Count(static x => x.ConsentState != ConsentState.Active);

    /// <summary>
    /// Raised when a property actually changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

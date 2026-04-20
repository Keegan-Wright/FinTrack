using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;

namespace FinanceTracker;

public sealed class ApplicationState : INotifyPropertyChanged
{
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}



public interface IBackgroundUpdateHub
{
    Task BackgroundBankingSyncComplete(DateTime completionTime);
}

public class BackgroundUpdateHub : Hub<IBackgroundUpdateHub>
{
}

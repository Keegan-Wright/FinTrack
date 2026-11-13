using FinanceTracker.Domain.Notifications;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Notifications;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Sharing;

/// <summary>
/// Records what's raised and who for, taking the user from its scope as the real notifier does, so a test can see a
/// notification went to the right person.
/// </summary>
internal sealed class RecordingNotifier : INotifier
{
    private readonly ICurrentUser _currentUser;
    private readonly List<(Guid UserId, NotificationKind Kind, string Title)> _raised;

    public RecordingNotifier(ICurrentUser currentUser, List<(Guid UserId, NotificationKind Kind, string Title)> raised)
    {
        _currentUser = currentUser;
        _raised = raised;
    }

    public Task<bool> NotifyAsync(NotificationKind kind, string key, string title, string message, string? link,
        CancellationToken cancellationToken)
    {
        _raised.Add((_currentUser.UserId, kind, title));
        return Task.FromResult(true);
    }
}

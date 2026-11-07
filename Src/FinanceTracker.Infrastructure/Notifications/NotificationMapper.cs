using ContractKind = FinanceTracker.Contracts.Notifications.NotificationKind;
using ContractNotification = FinanceTracker.Contracts.Notifications.Notification;
using DomainKind = FinanceTracker.Domain.Notifications.NotificationKind;
using DomainNotification = FinanceTracker.Domain.Notifications.Notification;

namespace FinanceTracker.Infrastructure.Notifications;

/// <summary>
/// Notifications and their kinds between the domain and the API.
/// </summary>
public static class NotificationMapper
{
    public static ContractNotification ToContract(DomainNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return new ContractNotification
        {
            Id = notification.Id,
            Kind = ToContract(notification.Kind),
            Title = notification.Title,
            Message = notification.Message,
            Link = notification.Link,
            RaisedAt = notification.Created,
            IsRead = notification.IsRead
        };
    }

    public static ContractKind ToContract(DomainKind kind) => kind switch
    {
        DomainKind.ConsentEndingSoon => ContractKind.ConsentEndingSoon,
        DomainKind.ConsentEnded => ContractKind.ConsentEnded,
        DomainKind.SyncFailed => ContractKind.SyncFailed,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public static DomainKind ToDomain(ContractKind kind) => kind switch
    {
        ContractKind.ConsentEndingSoon => DomainKind.ConsentEndingSoon,
        ContractKind.ConsentEnded => DomainKind.ConsentEnded,
        ContractKind.SyncFailed => DomainKind.SyncFailed,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}

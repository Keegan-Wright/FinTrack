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
        DomainKind.BillLate => ContractKind.BillLate,
        DomainKind.BillChanged => ContractKind.BillChanged,
        DomainKind.BillDueSoon => ContractKind.BillDueSoon,
        DomainKind.BudgetNearLimit => ContractKind.BudgetNearLimit,
        DomainKind.BudgetOverLimit => ContractKind.BudgetOverLimit,
        DomainKind.LargeTransaction => ContractKind.LargeTransaction,
        DomainKind.LowBalance => ContractKind.LowBalance,
        DomainKind.SharingInvite => ContractKind.SharingInvite,
        DomainKind.SharingAccepted => ContractKind.SharingAccepted,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public static DomainKind ToDomain(ContractKind kind) => kind switch
    {
        ContractKind.ConsentEndingSoon => DomainKind.ConsentEndingSoon,
        ContractKind.ConsentEnded => DomainKind.ConsentEnded,
        ContractKind.SyncFailed => DomainKind.SyncFailed,
        ContractKind.BillLate => DomainKind.BillLate,
        ContractKind.BillChanged => DomainKind.BillChanged,
        ContractKind.BillDueSoon => DomainKind.BillDueSoon,
        ContractKind.BudgetNearLimit => DomainKind.BudgetNearLimit,
        ContractKind.BudgetOverLimit => DomainKind.BudgetOverLimit,
        ContractKind.LargeTransaction => DomainKind.LargeTransaction,
        ContractKind.LowBalance => DomainKind.LowBalance,
        ContractKind.SharingInvite => DomainKind.SharingInvite,
        ContractKind.SharingAccepted => DomainKind.SharingAccepted,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}

using FinanceTracker.Emails;
using FinanceTracker.Infrastructure.Notifications;
using DomainKind = FinanceTracker.Domain.Notifications.NotificationKind;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Notifications;

/// <summary>
/// Every kind is carried to the API and back, and has its own look in an email, so a kind can't be half added.
/// </summary>
public class NotificationKindsTests
{
    [Test]
    public async Task EveryKind_MapsToTheApiAndBack()
    {
        using (Assert.Multiple())
        {
            foreach (DomainKind kind in Enum.GetValues<DomainKind>())
            {
                await Assert.That(NotificationMapper.ToDomain(NotificationMapper.ToContract(kind))).IsEqualTo(kind);
            }
        }
    }

    [Test]
    public async Task EveryKind_HasItsOwnLabelInAnEmail()
    {
        using (Assert.Multiple())
        {
            foreach (DomainKind kind in Enum.GetValues<DomainKind>())
            {
                await Assert.That(NotificationKindStyle.Label(kind)).IsNotEqualTo("Notification").Because(kind.ToString());
            }
        }
    }
}

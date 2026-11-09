using FinanceTracker.Client.Components.Notifications;
using FinanceTracker.Contracts.Notifications;
using MudBlazor;

namespace FinanceTracker.Tests.Unit.Client.Notifications;

/// <summary>
/// Every kind reads as words and has its own icon, so a kind added later isn't shown by its enum name.
/// </summary>
public class NotificationKindTextTests
{
    [Test]
    public async Task EveryKind_HasWordsAndAnIcon()
    {
        using (Assert.Multiple())
        {
            foreach (NotificationKind kind in NotificationKindText.All)
            {
                await Assert.That(NotificationKindText.Label(kind)).IsNotEqualTo(kind.ToString());
                await Assert.That(NotificationKindText.Icon(kind)).IsNotEqualTo(Icons.Material.Outlined.Notifications)
                    .Because(kind.ToString());
            }
        }
    }
}

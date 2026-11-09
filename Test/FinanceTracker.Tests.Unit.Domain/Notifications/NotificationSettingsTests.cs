using FinanceTracker.Domain.Notifications;

namespace FinanceTracker.Tests.Unit.Domain.Notifications;

[Category("Domain")]
public class NotificationSettingsTests
{
    [Test]
    public async Task NotificationSettings_New_EmailsEveryKind()
    {
        // Act
        NotificationSettings settings = new(Guid.NewGuid());

        // Assert
        await Assert.That(settings.Emailed).IsEquivalentTo(Enum.GetValues<NotificationKind>());
    }

    [Test]
    public async Task EmailOnly_SomeKindsRepeated_EmailsJustThoseOnce()
    {
        // Arrange
        NotificationSettings settings = new(Guid.NewGuid());

        // Act
        settings.EmailOnly([NotificationKind.SyncFailed, NotificationKind.SyncFailed, NotificationKind.ConsentEnded]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.Emailed).IsEquivalentTo([NotificationKind.ConsentEnded, NotificationKind.SyncFailed]);
            await Assert.That(settings.IsEmailed(NotificationKind.ConsentEndingSoon)).IsFalse();
        }
    }

    [Test]
    public async Task IsEmailedFor_NeverChanged_EmailsEveryKind()
    {
        // Act, Assert
        await Assert.That(Enum.GetValues<NotificationKind>()
            .All(static x => NotificationSettings.IsEmailedFor(null, x))).IsTrue();
    }

    [Test]
    public async Task RecordEmail_SentThenFailed_KeepsTheLatest()
    {
        // Arrange
        NotificationSettings settings = new(Guid.NewGuid());
        DateTime sent = new(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc);
        DateTime failed = sent.AddHours(1);

        // Act
        settings.RecordEmail(sent, null);
        settings.RecordEmail(failed, "  The mail server didn't accept FinTrack's sign in. ");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.LastEmailAt).IsEqualTo(failed);
            await Assert.That(settings.LastEmailFailure).IsEqualTo("The mail server didn't accept FinTrack's sign in.");
        }
    }

    [Test]
    public async Task RecordEmail_SentAfterFailing_ClearsTheFailure()
    {
        // Arrange
        NotificationSettings settings = new(Guid.NewGuid());
        settings.RecordEmail(new DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc), "It failed.");

        // Act
        settings.RecordEmail(new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc), null);

        // Assert
        await Assert.That(settings.LastEmailFailure).IsNull();
    }

    [Test]
    public async Task EmailOnly_SomeKinds_KeepsTheOthersAsTurnedOff()
    {
        // Arrange
        NotificationSettings settings = new(Guid.NewGuid());

        // Act
        settings.EmailOnly([NotificationKind.SyncFailed]);

        // Assert, kept as turned off, so a kind added later is emailed until they turn it off too
        await Assert.That(settings.NotEmailed).IsEquivalentTo(
            [NotificationKind.ConsentEndingSoon, NotificationKind.ConsentEnded]);
    }

    [Test]
    public async Task NotificationSettings_New_HasNoWebhookSoPostsNothing()
    {
        // Act
        NotificationSettings settings = new(Guid.NewGuid());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.HasWebhook).IsFalse();
            await Assert.That(settings.Webhooked).IsEquivalentTo(Enum.GetValues<NotificationKind>());
            await Assert.That(Enum.GetValues<NotificationKind>().Any(settings.IsWebhooked)).IsFalse();
        }
    }

    [Test]
    public async Task SetWebhook_First_PostsEveryKindSignedWithTheSecret()
    {
        // Arrange
        NotificationSettings settings = new(Guid.NewGuid());

        // Act
        settings.SetWebhook(" https://hooks.example.test/fintrack ", "secret-one");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.WebhookUrl).IsEqualTo("https://hooks.example.test/fintrack");
            await Assert.That(settings.WebhookSecret).IsEqualTo("secret-one");
            await Assert.That(Enum.GetValues<NotificationKind>().All(settings.IsWebhooked)).IsTrue();
        }
    }

    [Test]
    public async Task SetWebhook_AddressChanged_KeepsTheSecretAndHowTheLastPostWent()
    {
        // Arrange
        NotificationSettings settings = new(Guid.NewGuid());
        settings.SetWebhook("https://hooks.example.test/one", "secret-one");
        settings.RecordWebhook(new DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc), null);

        // Act
        settings.SetWebhook("https://hooks.example.test/two", "secret-two");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.WebhookUrl).IsEqualTo("https://hooks.example.test/two");
            await Assert.That(settings.WebhookSecret).IsEqualTo("secret-one");
            await Assert.That(settings.LastWebhookAt).IsNotNull();
        }
    }

    [Test]
    public async Task WebhookOnly_SomeKinds_PostsJustThose()
    {
        // Arrange
        NotificationSettings settings = new(Guid.NewGuid());
        settings.SetWebhook("https://hooks.example.test/fintrack", "secret");

        // Act
        settings.WebhookOnly([NotificationKind.ConsentEnded]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.IsWebhooked(NotificationKind.ConsentEnded)).IsTrue();
            await Assert.That(settings.IsWebhooked(NotificationKind.SyncFailed)).IsFalse();
            await Assert.That(settings.IsEmailed(NotificationKind.SyncFailed)).IsTrue();
        }
    }

    [Test]
    public async Task RemoveWebhook_ThenSetAnother_StartsAfreshButKeepsTheKindsChosen()
    {
        // Arrange
        NotificationSettings settings = new(Guid.NewGuid());
        settings.SetWebhook("https://hooks.example.test/one", "secret-one");
        settings.WebhookOnly([NotificationKind.SyncFailed]);
        settings.RecordWebhook(new DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc), "It failed.");

        // Act
        settings.RemoveWebhook();
        bool postedWhileRemoved = settings.IsWebhooked(NotificationKind.SyncFailed);
        settings.SetWebhook("https://hooks.example.test/two", "secret-two");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(postedWhileRemoved).IsFalse();
            await Assert.That(settings.WebhookSecret).IsEqualTo("secret-two");
            await Assert.That(settings.LastWebhookAt).IsNull();
            await Assert.That(settings.LastWebhookFailure).IsNull();
            await Assert.That(settings.Webhooked).IsEquivalentTo([NotificationKind.SyncFailed]);
        }
    }

    [Test]
    public async Task ReplaceWebhookSecret_WithAndWithoutAWebhook_ReplacesOnlyWhenThereIsOne()
    {
        // Arrange
        NotificationSettings without = new(Guid.NewGuid());
        NotificationSettings with = new(Guid.NewGuid());
        with.SetWebhook("https://hooks.example.test/fintrack", "secret-one");

        // Act
        bool replacedWithout = without.ReplaceWebhookSecret("secret-two");
        bool replacedWith = with.ReplaceWebhookSecret("secret-two");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(replacedWithout).IsFalse();
            await Assert.That(without.WebhookSecret).IsNull();
            await Assert.That(replacedWith).IsTrue();
            await Assert.That(with.WebhookSecret).IsEqualTo("secret-two");
        }
    }
}

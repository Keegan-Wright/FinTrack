using FinanceTracker.Domain.Notifications;

namespace FinanceTracker.Tests.Unit.Domain.Notifications;

[Category("Domain")]
public class NotificationSettingsTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

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
            Enum.GetValues<NotificationKind>().Where(static x => x != NotificationKind.SyncFailed));
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

    [Test]
    public async Task NotificationSettings_New_ShowsEveryKindAndStartsAlertsAtTheDefaults()
    {
        // Act
        NotificationSettings settings = new(Guid.NewGuid());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.InApp).IsEquivalentTo(Enum.GetValues<NotificationKind>());
            await Assert.That(settings.BudgetWarningPercent).IsEqualTo(80);
            await Assert.That(settings.BillReminderDays).IsEqualTo(3);
            await Assert.That(settings.LargeTransactionOver).IsNull();
            await Assert.That(settings.LowBalanceAlerts).IsEmpty();
        }
    }

    [Test]
    public async Task ShowInAppOnly_SomeKinds_KeepsTheOthersAsTurnedOff()
    {
        // Arrange
        NotificationSettings settings = new(Guid.NewGuid());

        // Act
        settings.ShowInAppOnly([NotificationKind.SyncFailed, NotificationKind.BillLate]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.InApp).IsEquivalentTo([NotificationKind.SyncFailed, NotificationKind.BillLate]);
            await Assert.That(settings.IsInApp(NotificationKind.BillDueSoon)).IsFalse();
            await Assert.That(NotificationSettings.IsInAppFor(null, NotificationKind.BillDueSoon)).IsTrue();
        }
    }

    [Test]
    public async Task ChangeAlerts_InRange_KeepsThem()
    {
        // Arrange
        NotificationSettings settings = new(Guid.NewGuid());

        // Act
        settings.ChangeAlerts(95, 1, 500m);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.BudgetWarningPercent).IsEqualTo(95);
            await Assert.That(settings.BillReminderDays).IsEqualTo(1);
            await Assert.That(settings.LargeTransactionOver).IsEqualTo(500m);
        }
    }

    [Test]
    [Arguments(0, 3, null)]
    [Arguments(101, 3, null)]
    [Arguments(80, 0, null)]
    [Arguments(80, NotificationSettings.MostBillReminderDays + 1, null)]
    [Arguments(80, 3, 0)]
    public async Task ChangeAlerts_OutOfRange_IsRefused(int percent, int days, int? over)
    {
        // Arrange
        NotificationSettings settings = new(Guid.NewGuid());

        // Act, Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
        {
            settings.ChangeAlerts(percent, days, over);
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task RecordBalance_GoesBelowThenStaysBelow_SaysSoOnce()
    {
        // Arrange
        Guid account = Guid.NewGuid();
        NotificationSettings settings = new(Guid.NewGuid());
        settings.WatchBalances(new Dictionary<Guid, decimal> { [account] = 100m });

        // Act
        bool above = settings.RecordBalance(account, 150m, Now);
        bool wentBelow = settings.RecordBalance(account, 80m, Now);
        bool stillBelow = settings.RecordBalance(account, 50m, Now.AddHours(4));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(above).IsFalse();
            await Assert.That(wentBelow).IsTrue();
            await Assert.That(stillBelow).IsFalse();
            await Assert.That(settings.LowBalanceAlerts.Single().LowSince).IsEqualTo(Now);
        }
    }

    [Test]
    public async Task RecordBalance_BackAboveThenBelowAgain_SaysSoAgain()
    {
        // Arrange
        Guid account = Guid.NewGuid();
        NotificationSettings settings = new(Guid.NewGuid());
        settings.WatchBalances(new Dictionary<Guid, decimal> { [account] = 100m });
        settings.RecordBalance(account, 80m, Now);

        // Act
        bool backAbove = settings.RecordBalance(account, 100m, Now.AddDays(1));
        bool belowAgain = settings.RecordBalance(account, 90m, Now.AddDays(2));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(backAbove).IsFalse();
            await Assert.That(belowAgain).IsTrue();
            await Assert.That(settings.LowBalanceAlerts.Single().LowSince).IsEqualTo(Now.AddDays(2));
        }
    }

    [Test]
    public async Task RecordBalance_AccountNotWatched_SaysNothing()
    {
        // Arrange
        NotificationSettings settings = new(Guid.NewGuid());

        // Act
        bool told = settings.RecordBalance(Guid.NewGuid(), -500m, Now);

        // Assert
        await Assert.That(told).IsFalse();
    }

    [Test]
    public async Task WatchBalances_AmountChanged_ChecksAfreshButKeepsOneUnchanged()
    {
        // Arrange
        Guid changed = Guid.NewGuid();
        Guid unchanged = Guid.NewGuid();
        Guid dropped = Guid.NewGuid();
        NotificationSettings settings = new(Guid.NewGuid());
        settings.WatchBalances(new Dictionary<Guid, decimal> { [changed] = 100m, [unchanged] = 50m, [dropped] = 10m });
        settings.RecordBalance(changed, 80m, Now);
        settings.RecordBalance(unchanged, 20m, Now);

        // Act
        settings.WatchBalances(new Dictionary<Guid, decimal> { [changed] = 70m, [unchanged] = 50m });

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(settings.LowBalanceAlerts.Select(static x => x.AccountId))
                .IsEquivalentTo([changed, unchanged]);
            await Assert.That(settings.LowBalanceAlerts.Single(x => x.AccountId == changed).LowSince).IsNull();
            await Assert.That(settings.LowBalanceAlerts.Single(x => x.AccountId == unchanged).LowSince)
                .IsEqualTo(Now);
        }
    }
}

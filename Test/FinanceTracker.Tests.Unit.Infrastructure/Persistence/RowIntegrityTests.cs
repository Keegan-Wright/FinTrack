using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Encryption;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Persistence;

/// <summary>
/// Row integrity tags, with the database changed by raw SQL as someone without the key would change it.
/// </summary>
public class RowIntegrityTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private const string OtherUsername = "secondary";

    [Before(Test)]
    public async Task SetUpAsync() =>
        await Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.UserScenario.WithUser(OtherUsername))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.BankAccountScenario.WithAccount("savings", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("rent", "main"))
            .Do(s => s.TransactionScenario.WithTransaction("food", "main"));

    [Test]
    public async Task Save_TagsTheRowUnderTheCurrentKey_AndItLoads()
    {
        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        List<string?> tags = await context.BankTransactions
            .Select(static x => EF.Property<string?>(x, RowIntegrity.TagProperty))
            .ToListAsync(CancellationToken);
        List<BankTransaction> loaded = await context.BankTransactions.ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(tags.All(static x => x?.StartsWith(RowIntegrity.TagPrefix(1), StringComparison.Ordinal) == true)).IsTrue();
            await Assert.That(loaded).Count().IsEqualTo(2);
        }
    }

    [Test]
    public async Task Load_OwnerChangedToAnotherUser_IsRefused()
    {
        // Arrange
        await SqlAsync("""
            UPDATE "BankConnections" SET "UserId" =
                (SELECT "Id" FROM "AspNetUsers" WHERE "Id" <> "BankConnections"."UserId" LIMIT 1)
            """);

        // Act, Assert
        RowIntegrityException? refused = await Assert.ThrowsAsync<RowIntegrityException>(LoadConnectionsAsync);
        await Assert.That(refused!.Table).IsEqualTo("BankConnections");
    }

    [Test]
    public async Task Load_TransactionMovedToAnotherAccount_IsRefused()
    {
        // Arrange
        await SqlAsync("""
            UPDATE "BankTransactions" SET "AccountId" =
                (SELECT "Id" FROM "BankAccounts" WHERE "Id" <> "BankTransactions"."AccountId" LIMIT 1)
            """);

        // Act, Assert
        await Assert.ThrowsAsync<RowIntegrityException>(LoadTransactionsAsync);
    }

    [Test]
    public async Task Load_EncryptedValueCopiedFromAnotherRow_IsRefused()
    {
        // Arrange, the value still decrypts, it's just in the wrong row
        await SqlAsync("""
            UPDATE "BankTransactions" SET "Description" =
                (SELECT "Description" FROM "BankTransactions" AS other WHERE other."Id" <> "BankTransactions"."Id")
            """);

        // Act, Assert
        await Assert.ThrowsAsync<RowIntegrityException>(LoadTransactionsAsync);
    }

    [Test]
    public async Task Load_RowGivenANewId_IsRefused()
    {
        // Arrange, as a row copied under a new id would be
        string rent = Scenario.TransactionMappings["rent"].ToString().ToUpperInvariant();
        string copy = Guid.NewGuid().ToString().ToUpperInvariant();
        await SqlAsync($"""UPDATE "BankTransactions" SET "Id" = '{copy}' WHERE "Id" = '{rent}'""");

        // Act, Assert
        await Assert.ThrowsAsync<RowIntegrityException>(LoadTransactionsAsync);
    }

    [Test]
    public async Task Load_BalanceCopiedFromAnotherAccount_IsRefused()
    {
        // Arrange, the balance lives in a type the account owns, stored in its row
        await Scenario.Do(s => s.BankAccountScenario.WithBalance("main"))
            .Do(s => s.BankAccountScenario.WithBalance("savings"));
        await SqlAsync("""
            UPDATE "BankAccounts" SET "Balance_Current" =
                (SELECT "Balance_Current" FROM "BankAccounts" AS other WHERE other."Id" <> "BankAccounts"."Id")
            """);

        // Act, Assert
        await Assert.ThrowsAsync<RowIntegrityException>(async () =>
        {
            await using FinanceTrackerContext context =
                await DbContextFactory.CreateDbContextAsync(CancellationToken);
            await context.BankAccounts.ToListAsync(CancellationToken);
        });
    }

    [Test]
    public async Task Load_JsonListCopiedFromAnotherUsersRow_IsRefused()
    {
        // Arrange, low balance alerts are stored as JSON in each user's settings row
        await AddSettingsAsync(Username, 100m);
        await AddSettingsAsync(OtherUsername, 5000m);
        await SqlAsync("""
            UPDATE "NotificationSettings" SET "LowBalanceAlerts" =
                (SELECT "LowBalanceAlerts" FROM "NotificationSettings" AS other
                 WHERE other."Id" <> "NotificationSettings"."Id")
            """);

        // Act, Assert
        await Assert.ThrowsAsync<RowIntegrityException>(async () =>
        {
            await using FinanceTrackerContext context =
                await DbContextFactory.CreateDbContextAsync(CancellationToken);
            await context.NotificationSettings.ToListAsync(CancellationToken);
        });
    }

    [Test]
    public async Task Load_JsonListReordered_IsRefused()
    {
        // Arrange, two alerts in one user's settings, their order swapped
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            NotificationSettings settings = new(Scenario.UserMappings[Username]);
            settings.WatchBalances(new Dictionary<Guid, decimal>
            {
                [Scenario.AccountMappings["main"]] = 100m, [Scenario.AccountMappings["savings"]] = 5000m
            });
            context.NotificationSettings.Add(settings);
            await context.SaveChangesAsync(CancellationToken);
        }

        await SqlAsync("""
            UPDATE "NotificationSettings" SET "LowBalanceAlerts" =
                json_array(json(json_extract("LowBalanceAlerts", '$[1]')), json(json_extract("LowBalanceAlerts", '$[0]')))
            """);

        // Act, Assert
        await Assert.ThrowsAsync<RowIntegrityException>(async () =>
        {
            await using FinanceTrackerContext context =
                await DbContextFactory.CreateDbContextAsync(CancellationToken);
            await context.NotificationSettings.ToListAsync(CancellationToken);
        });
    }

    [Test]
    public async Task Load_AuthenticatorKeyMovedToAnotherUser_IsRefused()
    {
        // Arrange, someone moving their own authenticator key onto another user to pass their two-factor check
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            context.UserTokens.Add(new IdentityUserToken<Guid>
            {
                UserId = Scenario.UserMappings[Username], LoginProvider = "[AspNetUserStore]",
                Name = "AuthenticatorKey", Value = "PLACEHOLDERKEY"
            });
            await context.SaveChangesAsync(CancellationToken);
        }

        await SqlAsync("""
            UPDATE "AspNetUserTokens" SET "UserId" =
                (SELECT "Id" FROM "AspNetUsers" WHERE "Id" <> "AspNetUserTokens"."UserId" LIMIT 1)
            """);

        // Act, Assert
        await Assert.ThrowsAsync<RowIntegrityException>(async () =>
        {
            await using FinanceTrackerContext context =
                await DbContextFactory.CreateDbContextAsync(CancellationToken);
            await context.UserTokens.ToListAsync(CancellationToken);
        });
    }

    [Test]
    public async Task Load_PasskeyMovedToAnotherUser_IsRefused()
    {
        // Arrange, someone moving their own passkey onto another user to sign in as them
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            context.UserPasskeys.Add(new IdentityUserPasskey<Guid>
            {
                CredentialId = [1, 2, 3, 4], UserId = Scenario.UserMappings[Username],
                Data = new IdentityPasskeyData
                {
                    PublicKey = [5, 6, 7, 8], Name = "Placeholder laptop", CreatedAt = DateTimeOffset.UnixEpoch,
                    Transports = [], AttestationObject = [9], ClientDataJson = [10]
                }
            });
            await context.SaveChangesAsync(CancellationToken);
        }

        await SqlAsync("""
            UPDATE "AspNetUserPasskeys" SET "UserId" =
                (SELECT "Id" FROM "AspNetUsers" WHERE "Id" <> "AspNetUserPasskeys"."UserId" LIMIT 1)
            """);

        // Act, Assert
        await Assert.ThrowsAsync<RowIntegrityException>(async () =>
        {
            await using FinanceTrackerContext context =
                await DbContextFactory.CreateDbContextAsync(CancellationToken);
            await context.UserPasskeys.ToListAsync(CancellationToken);
        });
    }

    [Test]
    public async Task Load_NoTagYet_IsLetThrough()
    {
        // Arrange, as a row written before tags
        await SqlAsync("""UPDATE "BankTransactions" SET "IntegrityTag" = NULL""");

        // Act
        List<BankTransaction> loaded = await LoadTransactionsAsync();

        // Assert
        await Assert.That(loaded).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Load_ChangedThroughTheApp_StillLoads()
    {
        // Arrange
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            BankTransaction transaction = await context.BankTransactions
                .SingleAsync(x => x.Id == Scenario.TransactionMappings["rent"], CancellationToken);
            context.Entry(transaction).Property(nameof(BankTransaction.Description)).CurrentValue = "RENT";
            await context.SaveChangesAsync(CancellationToken);
        }

        // Act
        List<BankTransaction> loaded = await LoadTransactionsAsync();

        // Assert
        await Assert.That(loaded.Select(static x => x.Description)).Contains("RENT");
    }

    [Test]
    public async Task Load_TaggedUnderAnOlderKeyStillInTheKeyring_Loads()
    {
        // Arrange, key 2 now current, the rows tagged under 1
        IDbContextFactory<FinanceTrackerContext> rotated =
            CreateDbContextFactory(new ColumnEncryptionService(TestEncryptionKeys.Keyring(currentKeyId: 2, 1, 2)));

        // Act
        await using FinanceTrackerContext context = await rotated.CreateDbContextAsync(CancellationToken);
        List<BankTransaction> loaded = await context.BankTransactions.ToListAsync(CancellationToken);

        // Assert
        await Assert.That(loaded).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Rotate_RowsWithoutTagsOrUnderAnOldKey_AreTaggedUnderTheCurrentOne()
    {
        // Arrange, one table written before tags, and key 2 made current
        await SqlAsync("""UPDATE "BankConnections" SET "IntegrityTag" = NULL""");
        IDbContextFactory<FinanceTrackerContext> rotated =
            CreateDbContextFactory(new ColumnEncryptionService(TestEncryptionKeys.Keyring(currentKeyId: 2, 1, 2)));

        // Act
        await new EncryptionKeyRotator(rotated, NullLogger<EncryptionKeyRotator>.Instance)
            .RotateAsync(CancellationToken);

        // Assert, and they still load under the new key alone
        await using FinanceTrackerContext context = await rotated.CreateDbContextAsync(CancellationToken);
        List<string?> connectionTags = await TagsAsync(context, "BankConnections");
        List<string?> transactionTags = await TagsAsync(context, "BankTransactions");
        IDbContextFactory<FinanceTrackerContext> onlyNewKey =
            CreateDbContextFactory(new ColumnEncryptionService(TestEncryptionKeys.Keyring(currentKeyId: 2)));
        await using FinanceTrackerContext newKeyContext = await onlyNewKey.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(connectionTags.All(static x => x?.StartsWith(RowIntegrity.TagPrefix(2), StringComparison.Ordinal) == true)).IsTrue();
            await Assert.That(transactionTags.All(static x => x?.StartsWith(RowIntegrity.TagPrefix(2), StringComparison.Ordinal) == true)).IsTrue();
            await Assert.That(await newKeyContext.BankConnections.CountAsync(CancellationToken)).IsEqualTo(1);
        }
    }

    [Test]
    public async Task Rotate_OwnedAndJsonValues_AreTaggedUnderTheNewKey_AndAThirdRunDoesNothing()
    {
        // Arrange, a balance stored in the account's row, alerts stored as JSON, and key 2 made current
        await Scenario.Do(s => s.BankAccountScenario.WithBalance("main"));
        await AddSettingsAsync(Username, 100m);
        IDbContextFactory<FinanceTrackerContext> rotated =
            CreateDbContextFactory(new ColumnEncryptionService(TestEncryptionKeys.Keyring(currentKeyId: 2, 1, 2)));
        EncryptionKeyRotator rotator = new(rotated, NullLogger<EncryptionKeyRotator>.Instance);

        // Act
        await rotator.RotateAsync(CancellationToken);
        int again = await rotator.RotateAsync(CancellationToken);

        // Assert, they load under the new key alone, and nothing stays stale, e.g. the account without a card
        IDbContextFactory<FinanceTrackerContext> onlyNewKey =
            CreateDbContextFactory(new ColumnEncryptionService(TestEncryptionKeys.Keyring(currentKeyId: 2)));
        await using FinanceTrackerContext context = await onlyNewKey.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(again).IsEqualTo(0);
            await Assert.That(await context.BankAccounts.CountAsync(CancellationToken)).IsEqualTo(2);
            await Assert.That((await context.NotificationSettings.SingleAsync(CancellationToken)).LowBalanceAlerts)
                .Count().IsEqualTo(1);
        }
    }

    [Test]
    public async Task Rotate_UserTokenWrittenBeforeTags_IsTagged()
    {
        // Arrange, an authenticator key saved before tags, in a table whose key is three columns
        await using (FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken))
        {
            context.UserTokens.Add(new IdentityUserToken<Guid>
            {
                UserId = Scenario.UserMappings[Username], LoginProvider = "[AspNetUserStore]",
                Name = "AuthenticatorKey", Value = "PLACEHOLDERKEY"
            });
            await context.SaveChangesAsync(CancellationToken);
        }

        await SqlAsync("""UPDATE "AspNetUserTokens" SET "IntegrityTag" = NULL""");

        // Act
        await new EncryptionKeyRotator(DbContextFactory, NullLogger<EncryptionKeyRotator>.Instance)
            .RotateAsync(CancellationToken);

        // Assert
        await using FinanceTrackerContext assertContext = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        List<string?> tags = await TagsAsync(assertContext, "AspNetUserTokens");
        await Assert.That(tags.All(static x => x?.StartsWith(RowIntegrity.TagPrefix(1), StringComparison.Ordinal) == true)).IsTrue();
    }

    [Test]
    public async Task Tag_FromAnotherKeyring_IsRefused()
    {
        // Arrange, the same content tagged with a key FinTrack doesn't have
        byte[] content = "content"u8.ToArray();
        string forged = new ColumnEncryptionService(TestEncryptionKeys.Keyring(currentKeyId: 9)).ComputeRowTag(content);
        string relabelled = "1" + forged[1..];

        // Act, Assert
        using (Assert.Multiple())
        {
            await Assert.That(ColumnEncryptionService.IsRowTagValid(ColumnEncryptionService.ComputeRowTag(content),
                content)).IsTrue();
            await Assert.That(ColumnEncryptionService.IsRowTagValid(forged, content)).IsFalse();
            await Assert.That(ColumnEncryptionService.IsRowTagValid(relabelled, content)).IsFalse();
            await Assert.That(ColumnEncryptionService.IsRowTagValid("not a tag", content)).IsFalse();
        }
    }

    [Test]
    [Arguments(LoadAll)]
    [Arguments(LoadAllUntracked)]
    [Arguments(LoadSingle)]
    [Arguments(LoadStreamed)]
    [Arguments(LoadFound)]
    public async Task Load_PasskeyDataSwappedWithAnotherUsers_IsRefused(string howLoaded)
    {
        // Arrange, the data is JSON, which EF fills in only after the row's been read, and still decrypts
        await AddPasskeyAsync(Username, 1, "Placeholder laptop");
        await AddPasskeyAsync(OtherUsername, 2, "Placeholder phone");
        await SqlAsync("""
            UPDATE "AspNetUserPasskeys" SET "Data" =
                (SELECT "Data" FROM "AspNetUserPasskeys" AS other
                 WHERE other."CredentialId" <> "AspNetUserPasskeys"."CredentialId")
            """);

        // Act, Assert
        RowIntegrityException? refused =
            await Assert.ThrowsAsync<RowIntegrityException>(() => LoadPasskeysAsync(howLoaded));
        await Assert.That(refused!.Table).IsEqualTo("AspNetUserPasskeys");
    }

    [Test]
    public async Task Load_PasskeyDataRefused_IsNotLeftTracked()
    {
        // Arrange
        await AddPasskeyAsync(Username, 1, "Placeholder laptop");
        await AddPasskeyAsync(OtherUsername, 2, "Placeholder phone");
        await SqlAsync("""
            UPDATE "AspNetUserPasskeys" SET "Data" =
                (SELECT "Data" FROM "AspNetUserPasskeys" AS other
                 WHERE other."CredentialId" <> "AspNetUserPasskeys"."CredentialId")
            """);
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);

        // Act
        await Assert.ThrowsAsync<RowIntegrityException>(() => context.UserPasskeys.ToListAsync(CancellationToken));

        // Assert, so it can't be saved back with a fresh tag
        await Assert.That(context.ChangeTracker.Entries<IdentityUserPasskey<Guid>>()).Count().IsLessThan(2);
    }

    [Test]
    public async Task Load_PasskeyUntouched_LoadsWithItsData()
    {
        // Arrange
        await AddPasskeyAsync(Username, 1, "Placeholder laptop");
        await AddPasskeyAsync(OtherUsername, 2, "Placeholder phone");

        // Act
        List<IdentityUserPasskey<Guid>> loaded = await LoadPasskeysAsync(LoadAllUntracked);

        // Assert
        await Assert.That(loaded.Select(static x => x.Data.Name ?? ""))
            .IsEquivalentTo(["Placeholder laptop", "Placeholder phone"]);
    }

    [Test]
    public async Task Load_TagFromAnOlderFormat_IsLetThrough()
    {
        // Arrange, as a row tagged before tags covered complex types
        await AddPasskeyAsync(Username, 1, "Placeholder laptop");
        await SqlAsync("""UPDATE "AspNetUserPasskeys" SET "IntegrityTag" = '1:AAAA'""");
        await SqlAsync("""UPDATE "BankTransactions" SET "IntegrityTag" = '1:AAAA'""");

        // Act
        List<IdentityUserPasskey<Guid>> passkeys = await LoadPasskeysAsync(LoadAll);
        List<BankTransaction> transactions = await LoadTransactionsAsync();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(passkeys).Count().IsEqualTo(1);
            await Assert.That(transactions).Count().IsEqualTo(2);
        }
    }

    [Test]
    public async Task Rotate_TagsFromAnOlderFormat_AreTaggedAgain_AndThenChecked()
    {
        // Arrange
        await AddPasskeyAsync(Username, 1, "Placeholder laptop");
        await AddPasskeyAsync(OtherUsername, 2, "Placeholder phone");
        await SqlAsync("""UPDATE "AspNetUserPasskeys" SET "IntegrityTag" = '1:AAAA'""");

        // Act
        await new EncryptionKeyRotator(DbContextFactory, NullLogger<EncryptionKeyRotator>.Instance)
            .RotateAsync(CancellationToken);
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        List<string?> tags = await TagsAsync(context, "AspNetUserPasskeys");
        await SqlAsync("""
            UPDATE "AspNetUserPasskeys" SET "Data" =
                (SELECT "Data" FROM "AspNetUserPasskeys" AS other
                 WHERE other."CredentialId" <> "AspNetUserPasskeys"."CredentialId")
            """);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(tags.All(static x => x?.StartsWith(RowIntegrity.TagPrefix(1), StringComparison.Ordinal) == true))
                .IsTrue();
            await Assert.ThrowsAsync<RowIntegrityException>(() => LoadPasskeysAsync(LoadAll));
        }
    }

    [Test]
    public async Task Rotate_PasskeyDataStoredAsJson_IsEncryptedUnderTheNewKey_AndAThirdRunDoesNothing()
    {
        // Arrange, key 2 made current
        await AddPasskeyAsync(Username, 1, "Placeholder laptop");
        IDbContextFactory<FinanceTrackerContext> rotated =
            CreateDbContextFactory(new ColumnEncryptionService(TestEncryptionKeys.Keyring(currentKeyId: 2, 1, 2)));
        EncryptionKeyRotator rotator = new(rotated, NullLogger<EncryptionKeyRotator>.Instance);

        // Act
        await rotator.RotateAsync(CancellationToken);
        int again = await rotator.RotateAsync(CancellationToken);

        // Assert, it loads under the new key alone
        IDbContextFactory<FinanceTrackerContext> onlyNewKey =
            CreateDbContextFactory(new ColumnEncryptionService(TestEncryptionKeys.Keyring(currentKeyId: 2)));
        await using FinanceTrackerContext context = await onlyNewKey.CreateDbContextAsync(CancellationToken);
        IdentityUserPasskey<Guid> passkey = await context.UserPasskeys.SingleAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(again).IsEqualTo(0);
            await Assert.That(passkey.Data.Name).IsEqualTo("Placeholder laptop");
            await Assert.That(passkey.Data.PublicKey).IsEquivalentTo(new byte[] { 5, 6, 7, 8 });
        }
    }

    [Test]
    public async Task Rotate_JsonPartlyRewrittenUnderTheNewKey_EndsWhollyUnderIt()
    {
        // Arrange, key 2 made current and the passkey renamed, which EF may save as an update to the name alone
        await AddPasskeyAsync(Username, 1, "Placeholder laptop");
        IDbContextFactory<FinanceTrackerContext> rotated =
            CreateDbContextFactory(new ColumnEncryptionService(TestEncryptionKeys.Keyring(currentKeyId: 2, 1, 2)));
        await using (FinanceTrackerContext context = await rotated.CreateDbContextAsync(CancellationToken))
        {
            IdentityUserPasskey<Guid> passkey = await context.UserPasskeys.SingleAsync(CancellationToken);
            passkey.Data.Name = "Renamed laptop";
            await context.SaveChangesAsync(CancellationToken);
        }

        // Act
        await new EncryptionKeyRotator(rotated, NullLogger<EncryptionKeyRotator>.Instance)
            .RotateAsync(CancellationToken);

        // Assert, nothing left under key 1, and it loads under key 2 alone
        await using FinanceTrackerContext assertContext = await rotated.CreateDbContextAsync(CancellationToken);
#pragma warning disable EF1002 // Test SQL with no input
        string json = await assertContext.Database
            .SqlQueryRaw<string>("""SELECT CAST("Data" AS TEXT) AS "Value" FROM "AspNetUserPasskeys" """)
            .SingleAsync(CancellationToken);
#pragma warning restore EF1002
        IDbContextFactory<FinanceTrackerContext> onlyNewKey =
            CreateDbContextFactory(new ColumnEncryptionService(TestEncryptionKeys.Keyring(currentKeyId: 2)));
        await using FinanceTrackerContext newKeyContext = await onlyNewKey.CreateDbContextAsync(CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(json).DoesNotContain("\":\"1:");
            await Assert.That((await newKeyContext.UserPasskeys.SingleAsync(CancellationToken)).Data.Name)
                .IsEqualTo("Renamed laptop");
        }
    }

    [Test]
    public async Task Rotate_JsonHoldingAnEmptyString_IsRewrittenOnce()
    {
        // Arrange, as the migration copies an empty value from a column into JSON. EF itself writes empty values as
        // null, so a rewrite clears the "" and the next run has nothing to do
        await AddPasskeyAsync(Username, 1, "Placeholder laptop");
        await SqlAsync("""UPDATE "AspNetUserPasskeys" SET "Data" = json_set("Data", '$.Aaguid', '')""");
        EncryptionKeyRotator rotator = new(DbContextFactory, NullLogger<EncryptionKeyRotator>.Instance);

        // Act
        int first = await rotator.RotateAsync(CancellationToken);
        int second = await rotator.RotateAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first).IsEqualTo(1);
            await Assert.That(second).IsEqualTo(0);
        }
    }

    private const string LoadAll = "all";
    private const string LoadAllUntracked = "all untracked";
    private const string LoadSingle = "single";
    private const string LoadStreamed = "streamed";
    private const string LoadFound = "found";

    private async Task<List<IdentityUserPasskey<Guid>>> LoadPasskeysAsync(string howLoaded)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        if (howLoaded == LoadAllUntracked)
        {
            return await context.UserPasskeys.AsNoTracking().ToListAsync(CancellationToken);
        }

        if (howLoaded == LoadSingle)
        {
            Guid userId = Scenario.UserMappings[Username];
            return [await context.UserPasskeys.SingleAsync(x => x.UserId == userId, CancellationToken)];
        }

        if (howLoaded == LoadStreamed)
        {
            List<IdentityUserPasskey<Guid>> streamed = [];
            await foreach (IdentityUserPasskey<Guid> passkey in context.UserPasskeys.AsNoTracking().AsAsyncEnumerable()
                               .WithCancellation(CancellationToken))
            {
                streamed.Add(passkey);
            }

            return streamed;
        }

        if (howLoaded == LoadFound)
        {
            return [(await context.UserPasskeys.FindAsync([new byte[] { 1 }], CancellationToken))!];
        }

        return await context.UserPasskeys.ToListAsync(CancellationToken);
    }

    private async Task AddPasskeyAsync(string username, byte credentialId, string name)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        context.UserPasskeys.Add(new IdentityUserPasskey<Guid>
        {
            CredentialId = [credentialId], UserId = Scenario.UserMappings[username],
            Data = new IdentityPasskeyData
            {
                PublicKey = [5, 6, 7, 8], Name = name, CreatedAt = DateTimeOffset.UnixEpoch,
                Transports = [], AttestationObject = [9], ClientDataJson = [10]
            }
        });
        await context.SaveChangesAsync(CancellationToken);
    }

    private async Task AddSettingsAsync(string username, decimal below)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        NotificationSettings settings = new(Scenario.UserMappings[username]);
        settings.WatchBalances(new Dictionary<Guid, decimal> { [Scenario.AccountMappings["main"]] = below });
        context.NotificationSettings.Add(settings);
        await context.SaveChangesAsync(CancellationToken);
    }

    private async Task SqlAsync(string sql)
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
#pragma warning disable EF1002 // Test SQL with no input
        await context.Database.ExecuteSqlRawAsync(sql, CancellationToken);
#pragma warning restore EF1002
    }

    private async Task<List<BankConnection>> LoadConnectionsAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.BankConnections.ToListAsync(CancellationToken);
    }

    private async Task<List<BankTransaction>> LoadTransactionsAsync()
    {
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        return await context.BankTransactions.ToListAsync(CancellationToken);
    }

    private Task<List<string?>> TagsAsync(FinanceTrackerContext context, string table) =>
#pragma warning disable EF1002 // Test SQL, the table is the test's own
        context.Database.SqlQueryRaw<string?>($"""SELECT "IntegrityTag" AS "Value" FROM "{table}" """)
            .ToListAsync(CancellationToken);
#pragma warning restore EF1002
}

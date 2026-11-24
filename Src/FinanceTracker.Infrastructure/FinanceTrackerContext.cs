using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.Calendar;
using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.Debts;
using FinanceTracker.Domain.Household;
using FinanceTracker.Domain.Identity;
using FinanceTracker.Domain.NetWorth;
using FinanceTracker.Domain.Notifications;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Balances;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.OpenBanking.Transfers;
using FinanceTracker.Domain.PayPeriods;
using FinanceTracker.Domain.Rules;
using FinanceTracker.Domain.SavingsGoals;
using FinanceTracker.Domain.Sharing;
using FinanceTracker.Infrastructure.Encryption;
using FinanceTracker.Infrastructure.Notifications.Push;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure;

/// <summary>
/// The app's database, Identity's tables with the users' data. Sensitive columns are encrypted, see
/// <see cref="EncryptionService"/>.
/// </summary>
public class FinanceTrackerContext : IdentityDbContext<FinanceTrackerUser, FinanceTrackerRole, Guid>,
    IDesignTimeDbContextFactory<FinanceTrackerContext>,
    IDataProtectionKeyContext
{
    /// <summary>
    /// How long a database command may run before it's abandoned.
    /// </summary>
    public const int CommandTimeoutSeconds = 30;

    private readonly IColumnEncryptionService _columnEncryptionService;

    /// <summary>
    /// Encrypts columns and computes blind indexes, see <see cref="Persistence.BlindIndexInterceptor"/>.
    /// </summary>
    internal IColumnEncryptionService EncryptionService => _columnEncryptionService;

    /// <summary>
    /// The clock <see cref="Persistence.AuditingInterceptor"/> stamps audit times from.
    /// </summary>
    internal TimeProvider TimeProvider { get; } = TimeProvider.System;

    /// <summary>
    /// Loaded rows whose integrity tags cover JSON, checked once it's filled in, see
    /// <see cref="RowIntegrityQueryCompiler"/>.
    /// </summary>
    internal PendingRowIntegrityChecks PendingRowIntegrityChecks { get; } = new();

    /// <summary>
    /// Whether a row tagged in an older format loads, set only while <see cref="EncryptionKeyRotator"/> tags such rows
    /// again at startup. Once it has, every row is at the current format, so any other context refuses one that isn't,
    /// see <see cref="RowIntegrityCheck"/>.
    /// </summary>
    internal bool AcceptsOutdatedRowTags { get; set; }

    /// <summary>
    /// Only for the design time tools, to reach <see cref="CreateDbContext"/>.
    /// </summary>
    [Obsolete("Used by design time only")]
#pragma warning disable CS8618 // Only used to reach CreateDbContext, which builds a fully initialised context
    public FinanceTrackerContext()
    {
    }
#pragma warning restore CS8618

    /// <param name="options">EF Core's options.</param>
    /// <param name="columnEncryptionService">Encrypts and decrypts columns.</param>
    /// <param name="timeProvider">The clock.</param>
    [ActivatorUtilitiesConstructor]
    public FinanceTrackerContext(DbContextOptions<FinanceTrackerContext> options,
        IColumnEncryptionService columnEncryptionService, TimeProvider timeProvider) : base(options)
    {
        _columnEncryptionService = columnEncryptionService;
        TimeProvider = timeProvider;
    }


    #region Aggregates

    /// <summary>
    /// Users' connections to their banks.
    /// </summary>
    public DbSet<BankConnection> BankConnections { get; private set; }

    /// <summary>
    /// The accounts and cards synced from the banks.
    /// </summary>
    public DbSet<BankAccount> BankAccounts { get; private set; }

    /// <summary>
    /// The transactions synced from the banks.
    /// </summary>
    public DbSet<BankTransaction> BankTransactions { get; private set; }

    /// <summary>
    /// Users' tags.
    /// </summary>
    public DbSet<CustomClassification> CustomClassifications { get; private set; }

    /// <summary>
    /// Users' savings goals.
    /// </summary>
    public DbSet<SavingsGoal> SavingsGoals { get; private set; }

    /// <summary>
    /// Users' budgets.
    /// </summary>
    public DbSet<Budget> Budgets { get; private set; }

    /// <summary>
    /// Users' bills.
    /// </summary>
    public DbSet<Bill> Bills { get; private set; }

    /// <summary>
    /// Repeat payments suggested as bills.
    /// </summary>
    public DbSet<BillSuggestion> BillSuggestions { get; private set; }

    /// <summary>
    /// Users' pay.
    /// </summary>
    public DbSet<PaySchedule> PaySchedules { get; private set; }

    /// <summary>
    /// Users' debts.
    /// </summary>
    public DbSet<Debt> Debts { get; private set; }

    /// <summary>
    /// Users' calendar events.
    /// </summary>
    public DbSet<CalendarEvent> CalendarEvents { get; private set; }

    /// <summary>
    /// Users' household members.
    /// </summary>
    public DbSet<HouseholdMember> HouseholdMembers { get; private set; }

    /// <summary>
    /// Links between two users who share.
    /// </summary>
    public DbSet<UserLink> UserLinks { get; private set; }

    /// <summary>
    /// What users share through their links.
    /// </summary>
    public DbSet<Share> Shares { get; private set; }

    /// <summary>
    /// How users see what's shared with them.
    /// </summary>
    public DbSet<SharingSettings> SharingSettings { get; private set; }

    /// <summary>
    /// Joint accounts two linked users both connected, merged.
    /// </summary>
    public DbSet<JointAccount> JointAccounts { get; private set; }

    /// <summary>
    /// When each user's transfers were last matched.
    /// </summary>
    public DbSet<TransferMatchCheckpoint> TransferMatchCheckpoints { get; private set; }

    /// <summary>
    /// Each account's closing balance by day.
    /// </summary>
    public DbSet<BalanceSnapshot> BalanceSnapshots { get; private set; }

    /// <summary>
    /// Each user's net worth by day.
    /// </summary>
    public DbSet<NetWorthSnapshot> NetWorthSnapshots { get; private set; }

    /// <summary>
    /// Users' rules.
    /// </summary>
    public DbSet<Rule> Rules { get; private set; }

    /// <summary>
    /// When each user's rules were last applied.
    /// </summary>
    public DbSet<RuleCheckpoint> RuleCheckpoints { get; private set; }

    /// <summary>
    /// What users have been notified of.
    /// </summary>
    public DbSet<Notification> Notifications { get; private set; }

    /// <summary>
    /// The tokens scripts read users' data with.
    /// </summary>
    public DbSet<PersonalAccessToken> PersonalAccessTokens { get; private set; }

    /// <summary>
    /// How users want to be notified.
    /// </summary>
    public DbSet<NotificationSettings> NotificationSettings { get; private set; }

    /// <summary>
    /// The browsers users get push notifications on.
    /// </summary>
    public DbSet<PushDevice> PushDevices { get; private set; }

    #endregion

    /// <summary>
    /// Append only, entries are created through <see cref="BankAccount.RecordSync"/>.
    /// </summary>
    public DbSet<SyncLogEntry> SyncLog { get; private set; }


    /// <summary>
    /// ASP.NET Data Protection's keys, which protect the sign in cookie and antiforgery tokens. Kept here so they
    /// survive redeploys, with their XML encrypted like any other sensitive column.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys { get; private set; }

    /// <summary>
    /// The server's key pair that signs push notifications, one row, see <see cref="StoredVapidKeys"/>.
    /// </summary>
    public DbSet<StoredVapidKeys> VapidKeys { get; private set; }


    /// <summary>
    /// The version of Identity's tables, the one with passkeys. Identity reads it from the app's settings, see
    /// <see cref="IdentitySchemaOptionsExtensions.UseIdentitySchema"/> for contexts built without them.
    /// </summary>
    public static Version IdentitySchemaVersion { get; } = IdentitySchemaVersions.Version3;

    /// <inheritdoc/>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(
            AuditingInterceptor.Instance,
            BlindIndexInterceptor.Instance,
            RowIntegrityInterceptor.Instance,
            RowIntegrityCheckInterceptor.Instance,
            ComplexJsonCollectionQueryInterceptor.Instance
            );

        optionsBuilder.ReplaceService<IModelCacheKeyFactory, EncryptionModelCacheKeyFactory>();
#pragma warning disable EF1001 // See RowIntegrityQueryCompiler
        optionsBuilder.ReplaceService<IQueryCompiler, RowIntegrityQueryCompiler>();
#pragma warning restore EF1001
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ThrowIfNotIdentitySchema();
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(FinanceTrackerContext).Assembly);

        builder.UseEncryption(_columnEncryptionService);
        builder.UseRowIntegrity();
    }

    /// <summary>
    /// Stops a context building a model without passkeys, which Identity quietly does when it isn't given the schema
    /// version.
    /// </summary>
    /// <exception cref="InvalidOperationException">The context's Identity settings aren't at
    /// <see cref="IdentitySchemaVersion"/>.</exception>
    private void ThrowIfNotIdentitySchema()
    {
        Version? version = this.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()
            ?.ApplicationServiceProvider?.GetService<IOptions<IdentityOptions>>()?.Value.Stores.SchemaVersion;
        if (version != IdentitySchemaVersion)
        {
            throw new InvalidOperationException(
                $"The context's Identity schema version is {version}, not {IdentitySchemaVersion}. Build its " +
                "options with the app's services or UseIdentitySchema().");
        }
    }

    /// <summary>
    /// Design time only, a context for making migrations, not connected to a real database.
    /// </summary>
    /// <param name="args">The design time tools' arguments, unused.</param>
    /// <returns>The context.</returns>
    public FinanceTrackerContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<FinanceTrackerContext> builder =
            new DbContextOptionsBuilder<FinanceTrackerContext>().UseIdentitySchema();
        builder.UseNpgsql("DesignTimeConnectionString", npgsqlDbContextOptionsBuilder =>
        {
            npgsqlDbContextOptionsBuilder.MigrationsAssembly("FinanceTracker.Infrastructure.Migrations");
            npgsqlDbContextOptionsBuilder.EnableRetryOnFailure();
            npgsqlDbContextOptionsBuilder.CommandTimeout(CommandTimeoutSeconds);
        });

        // Migrations only need the model, a throwaway key is enough
        return new FinanceTrackerContext(builder.Options, new ColumnEncryptionService(new EncryptionConfiguration
        {
            Keys = new Dictionary<int, byte[]> { [1] = new byte[EncryptionConfiguration.KeySize] },
            CurrentKeyId = 1
        }), TimeProvider.System);
    }
}

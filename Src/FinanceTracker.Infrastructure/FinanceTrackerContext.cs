using FinanceTracker.Domain.Identity;
using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.Classifications;
using FinanceTracker.Domain.Household;
using FinanceTracker.Domain.OpenBanking.Accounts;
using FinanceTracker.Domain.OpenBanking.Connections;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure.Encryption;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using TickerQ.EntityFrameworkCore.Configurations;
using TickerQ.Utilities.Entities;

namespace FinanceTracker.Infrastructure;

public class FinanceTrackerContext : IdentityDbContext<FinanceTrackerUser, FinanceTrackerRole, Guid>, IDesignTimeDbContextFactory<FinanceTrackerContext>
{
    private readonly ISymmetricEncryptionService _symmetricEncryptionService;

    [Obsolete("Used by design time only")]
    public FinanceTrackerContext()
    {

    }

    [ActivatorUtilitiesConstructor]
    public FinanceTrackerContext(DbContextOptions<FinanceTrackerContext> options,
        ISymmetricEncryptionService symmetricEncryptionService) : base(options)
    {
        _symmetricEncryptionService = symmetricEncryptionService;
    }


    #region Aggregates
    public DbSet<BankConnection> BankConnections { get; private set; }
    public DbSet<BankAccount> BankAccounts { get; private set; }
    public DbSet<BankTransaction> BankTransactions { get; private set; }
    public DbSet<CustomClassification> CustomClassifications { get; private set; }
    public DbSet<BudgetCategory> BudgetCategories { get; private set; }
    public DbSet<Debt> Debts { get; private set; }
    public DbSet<HouseholdMember> HouseholdMembers { get; private set; }
    #endregion

    /// <summary>
    /// Append only, entries are created through <see cref="BankAccount.RecordSync"/>.
    /// </summary>
    public DbSet<SyncLogEntry> SyncLog { get; private set; }


    #region TickerQ
    public DbSet<CronTickerEntity> CronTickerEntities { get; private set; }
    public DbSet<CronTickerOccurrenceEntity<CronTickerEntity>> CronTickerOccurrenceEntities { get; private set; }
    public DbSet<TimeTickerEntity> TimeTickerEntities { get; private set; }
    #endregion


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(AuditingInterceptor.Instance);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new TimeTickerConfigurations<TimeTickerEntity>("ticker"));
        builder.ApplyConfiguration(new CronTickerConfigurations<CronTickerEntity>("ticker"));
        builder.ApplyConfiguration(new CronTickerOccurrenceConfigurations<CronTickerEntity>("ticker"));

        builder.ApplyConfigurationsFromAssembly(typeof(FinanceTrackerContext).Assembly);

        builder.UseEncryption(_symmetricEncryptionService);

    }

    /// <summary>
    /// Design time only - creates a mock instance of this context for migration creation
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public FinanceTrackerContext CreateDbContext(string[] args)
    {
            var builder = new DbContextOptionsBuilder<FinanceTrackerContext>();
            builder.UseNpgsql("DesignTimeConnectionString", npgsqlDbContextOptionsBuilder =>
            {
                npgsqlDbContextOptionsBuilder.MigrationsAssembly("FinanceTracker.Infrastructure.Migrations");
                npgsqlDbContextOptionsBuilder.EnableRetryOnFailure();
                npgsqlDbContextOptionsBuilder.CommandTimeout(0);
            });

            return new FinanceTrackerContext(builder.Options, new SymmetricEncryptionService(new EncryptionConfiguration()
            {
                SymmetricKey = "DesignTimeKey",
                SymmetricSalt = "DesignTimeSalt",
                Iterations = 1
            }));
        }
}

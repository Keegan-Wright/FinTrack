using System.Reflection;
using FinanceTracker.Domain;
using FinanceTracker.Domain.Utility;
using FinanceTracker.Infrastructure.Encryption;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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


    public DbSet<FinanceTrackerUser> FinanceTrackerUsers { get; private set; }
    public DbSet<FinanceTrackerRole> FinanceTrackerRoles { get; private set; }
    public DbSet<BudgetCategory> BudgetCategories { get; private set; }
    public DbSet<Debt> Debts { get; private set; }
    public DbSet<OpenBankingProvider> OpenBankingProviders { get; private set; }
    public DbSet<OpenBankingProviderScopes> OpenBankingProviderScopes { get; private set; }
    public DbSet<OpenBankingAccount> OpenBankingAccounts { get; private set; }
    public DbSet<OpenBankingAccountBalance> OpenBankingAccountBalances { get; private set; }
    public DbSet<OpenBankingTransaction> OpenBankingTransactions { get; private set; }
    public DbSet<OpenBankingAccessToken> OpenBankingAccessTokens { get; private set; }
    public DbSet<OpenBankingStandingOrder> OpenBankingStandingOrders { get; private set; }
    public DbSet<OpenBankingSynchronization> OpenBankingSynchronizations { get; private set; }
    public DbSet<OpenBankingDirectDebit> OpenBankingDirectDebits { get; private set; }
    public DbSet<OpenBankingTransactionClassifications> OpenBankingTransactionClassifications { get; private set; }
    public DbSet<CustomClassification> CustomClassifications { get; private set; }
    public DbSet<HouseholdMember> HouseholdMembers { get; private set; }


    public DbSet<CronTickerEntity> CronTickerEntities { get; private set; }
    public DbSet<CronTickerOccurrenceEntity<CronTickerEntity>> CronTickerOccurrenceEntities { get; private set; }
    public DbSet<TimeTickerEntity> TimeTickerEntities { get; private set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfiguration(new TimeTickerConfigurations<TimeTickerEntity>("ticker"));
        builder.ApplyConfiguration(new CronTickerConfigurations<CronTickerEntity>("ticker"));
        builder.ApplyConfiguration(new CronTickerOccurrenceConfigurations<CronTickerEntity>("ticker"));

        foreach (IMutableEntityType entityType in builder.Model.GetEntityTypes())
        {
            Type clrType = entityType.ClrType;
            foreach (PropertyInfo property in clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!Attribute.IsDefined(property, typeof(EncryptAttribute)))
                {
                    continue;
                }

                Type converterType = typeof(EncryptionConverter<>).MakeGenericType(property.PropertyType);
                ValueConverter? converter =
                    Activator.CreateInstance(converterType, _symmetricEncryptionService) as ValueConverter;
                builder.Entity(clrType).Property(property.Name).HasConversion(converter);
            }
        }

        base.OnModelCreating(builder);
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

public class EncryptionConverter<TModel> : ValueConverter<TModel, string>
{
    public EncryptionConverter(ISymmetricEncryptionService symmetricEncryptionService) : base(
        v => symmetricEncryptionService.Encrypt(v),
        v => symmetricEncryptionService.Decrypt<TModel>(v)!)
    {
    }
}

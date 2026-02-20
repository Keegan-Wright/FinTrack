using FinanceTracker.Data.Models;
using FinanceTracker.Security.Encryption;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Reflection;
using System.Reflection.Emit;
using FinanceTracker.Data.Models.Utility;

namespace FinanceTracker.Data;

public class FinanceTrackerContext : IdentityDbContext<FinanceTrackerUser, FinanceTrackerRole, Guid>
{
    private readonly ISymmetricEncryptionService _symmetricEncryptionService;
    public FinanceTrackerContext(DbContextOptions<FinanceTrackerContext> options, ISymmetricEncryptionService symmetricEncryptionService) : base(options)
    {
        _symmetricEncryptionService = symmetricEncryptionService;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            foreach (var property in clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (Attribute.IsDefined(property, typeof(EncryptAttribute)))
                {
                    var converterType = typeof(EncryptionConverter<>).MakeGenericType(property.PropertyType);
                    var converter = (ValueConverter)Activator.CreateInstance(converterType, _symmetricEncryptionService);
                    builder.Entity(clrType).Property(property.Name).HasConversion(converter);
                }
            }
        }
        
        base.OnModelCreating(builder);
    }


    public DbSet<FinanceTrackerUser> FinanceTrackerUsers { get; set; }
    public DbSet<FinanceTrackerRole> FinanceTrackerRoles { get; set; }
    public DbSet<BudgetCategory> BudgetCategories { get; set; }
    public DbSet<Debt> Debts { get; set; }
    public DbSet<OpenBankingProvider> OpenBankingProviders { get; set; }
    public DbSet<OpenBankingProviderScopes> OpenBankingProviderScopes { get; set; }
    public DbSet<OpenBankingAccount> OpenBankingAccounts { get; set; }
    public DbSet<OpenBankingAccountBalance> OpenBankingAccountBalances { get; set; }
    public DbSet<OpenBankingTransaction> OpenBankingTransactions { get; set; }
    public DbSet<OpenBankingAccessToken> OpenBankingAccessTokens { get; set; }
    public DbSet<OpenBankingStandingOrder> OpenBankingStandingOrders { get; set; }
    public DbSet<OpenBankingSynchronization> OpenBankingSynchronizations { get; set; }
    public DbSet<OpenBankingDirectDebit> OpenBankingDirectDebits { get; set; }
    public DbSet<OpenBankingTransactionClassifications> OpenBankingTransactionClassifications { get; set; }
    public DbSet<CustomClassification> CustomClassifications { get; set; }
    public DbSet<HouseholdMember> HouseholdMembers { get; set; }
}

public class EncryptionConverter<TModel> : ValueConverter<TModel, string>
{
    public EncryptionConverter(ISymmetricEncryptionService symmetricEncryptionService) : base(
        v => symmetricEncryptionService.Encrypt(v),
        v => symmetricEncryptionService.Decrypt<TModel>(v))
    {
    }

}

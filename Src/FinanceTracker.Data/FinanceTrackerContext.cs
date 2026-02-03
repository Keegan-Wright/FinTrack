using FinanceTracker.Data.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Data;

public class FinanceTrackerContext : IdentityDbContext<FinanceTrackerUser, FinanceTrackerRole, Guid>
{
    public FinanceTrackerContext(DbContextOptions<FinanceTrackerContext> options) : base(options)
    {
        
    }
    
    public DbSet<FinanceTrackerUser> FinanceTrackerUsers { get; set; }
    public DbSet<FinanceTrackerRole> FinanceTrackerRoles { get; set; }
    
    public DbSet<HouseholdMember> HouseholdMembers { get; set; }
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
}
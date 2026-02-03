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
    
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinanceTracker.Ai.Persistence;

/// <summary>
/// Makes <see cref="FinanceTrackerAiContext"/> for <c>dotnet ef</c>, so its migrations are made from this project alone, without
/// building the app with the AI module in. Never connects, migrations are only generated here.
/// </summary>
public class FinanceTrackerAiContextDesignTimeFactory : IDesignTimeDbContextFactory<FinanceTrackerAiContext>
{
    /// <inheritdoc/>
    public FinanceTrackerAiContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<FinanceTrackerAiContext> options = new();
        FinanceTrackerAiContextOptions.Configure(options, "Host=localhost;Database=design-time");
        return new FinanceTrackerAiContext(options.Options);
    }
}

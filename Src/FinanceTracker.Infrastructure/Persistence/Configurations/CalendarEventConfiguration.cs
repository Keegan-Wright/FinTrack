using FinanceTracker.Domain.Calendar;
using FinanceTracker.Domain.SavingsGoals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class CalendarEventConfiguration : AggregateRootConfiguration<CalendarEvent>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.ToTable("CalendarEvents");

        builder.Property(x => x.Title).Encrypted();
        builder.Property(x => x.Date).Encrypted();
        builder.Property(x => x.Recurrence).Encrypted();
        builder.Property(x => x.Notes).Encrypted();
        builder.Property(x => x.SavingsTarget).Encrypted();

        // Outlives its goal, it just stops saving in one. Rolling on is kept in the clear so the daily job can find
        // the events that do
        builder.HasOne<SavingsGoal>()
            .WithMany()
            .HasForeignKey(x => x.SavingsGoalId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

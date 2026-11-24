using FinanceTracker.Domain.Household;
using FinanceTracker.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="HouseholdMember"/> to its table.
/// </summary>
internal sealed class HouseholdMemberConfiguration : AggregateRootConfiguration<HouseholdMember>
{
    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<HouseholdMember> builder)
    {
        builder.ToTable("HouseholdMembers");

        builder.Property(x => x.FirstName).Encrypted();
        builder.Property(x => x.LastName).Encrypted();
        builder.Property(x => x.YearlyIncome).HasColumnName("Income").Encrypted();

        // One person per first and last name for each user in any case, see NameCheck
        string nameKey = builder.HasNameBlindIndex(x => x.FirstName, x => x.LastName);
        builder.HasIndex(nameof(HouseholdMember.UserId), nameKey).IsUnique();

        // Unlinked when that user goes, the member stays as a plain name. One member per user for each owner
        builder.HasOne<FinanceTrackerUser>()
            .WithMany()
            .HasForeignKey(x => x.LinkedUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.UserId, x.LinkedUserId }).IsUnique().HasFilter("\"LinkedUserId\" IS NOT NULL");

        builder.Property(x => x.PayeeNames).HasField("_payeeNames").Encrypted();

        builder.ComplexJsonCollection(x => x.LinkedAccounts, account =>
        {
            account.Property(x => x.SortCode).Encrypted();
            account.Property(x => x.Number).Encrypted();
            account.Property(x => x.Iban).Encrypted();
        });
    }
}

using FinanceTracker.Domain.Household;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class HouseholdMemberConfiguration : AggregateRootConfiguration<HouseholdMember>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<HouseholdMember> builder)
    {
        builder.ToTable("HouseholdMembers");

        builder.Property(x => x.FirstName).Encrypted();
        builder.Property(x => x.LastName).Encrypted();
        builder.Property(x => x.Income).Encrypted();

        builder.Property(x => x.PayeeNames).Encrypted();
        builder.PrimitiveCollection(x => x.PayeeNames).HasField("_payeeNames");

        // Its own table, which the key rotator re-encrypts along with the member as owned rows load with their owner
        builder.OwnsMany(x => x.LinkedAccounts, account =>
        {
            account.ToTable("HouseholdMemberLinkedAccounts");
            // Generated on the client, the database doesn't generate the default int key of a composite one
            account.Property<Guid>("Id").ValueGeneratedOnAdd();
            account.HasKey("Id");
            account.Property(x => x.SortCode).Encrypted();
            account.Property(x => x.Number).Encrypted();
            account.Property(x => x.Iban).Encrypted();
        });
        builder.Navigation(x => x.LinkedAccounts).HasField("_linkedAccounts");
    }
}

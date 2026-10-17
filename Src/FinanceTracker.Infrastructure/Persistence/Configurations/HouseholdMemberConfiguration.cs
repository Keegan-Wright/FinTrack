using FinanceTracker.Domain.Household;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations;

internal sealed class HouseholdMemberConfiguration : AggregateRootConfiguration<HouseholdMember>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<HouseholdMember> builder) =>
        builder.ToTable("HouseholdMembers");
}

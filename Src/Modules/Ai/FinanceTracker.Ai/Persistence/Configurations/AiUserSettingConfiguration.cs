using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Ai.Persistence.Configurations;

/// <summary>
/// Maps <see cref="AiUserSetting"/> to its table.
/// </summary>
internal sealed class AiUserSettingConfiguration : IEntityTypeConfiguration<AiUserSetting>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AiUserSetting> builder)
    {
        builder.ToTable("UserSettings");

        builder.HasKey(x => x.UserId);
    }
}

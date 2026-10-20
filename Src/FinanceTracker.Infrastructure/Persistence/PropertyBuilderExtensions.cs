using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence;

public static class PropertyBuilderExtensions
{

    public static PropertyBuilder<TProperty> Encrypted<TProperty>(this PropertyBuilder<TProperty> propertyBuilder)
    {
        ArgumentNullException.ThrowIfNull(propertyBuilder);

        propertyBuilder.HasAnnotation(Annotations.EncryptedPropertyAnnotation, true);

        return propertyBuilder;
    }
}

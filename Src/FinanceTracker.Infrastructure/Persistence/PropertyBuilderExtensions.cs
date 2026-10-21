using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence;

public static class PropertyBuilderExtensions
{

    extension<TProperty>(PropertyBuilder<TProperty> propertyBuilder)
    {
        public PropertyBuilder<TProperty> Encrypted()
        {
            ArgumentNullException.ThrowIfNull(propertyBuilder);

            propertyBuilder.HasAnnotation(Annotations.EncryptedPropertyAnnotation, true);

            return propertyBuilder;
        }
    }
}

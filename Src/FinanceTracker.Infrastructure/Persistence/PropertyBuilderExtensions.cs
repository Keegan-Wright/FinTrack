using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// Marks properties to be encrypted.
/// </summary>
public static class PropertyBuilderExtensions
{
    extension<TProperty>(PropertyBuilder<TProperty> propertyBuilder)
    {
        /// <summary>
        /// Marks the property to be encrypted at rest.
        /// </summary>
        public PropertyBuilder<TProperty> Encrypted()
        {
            ArgumentNullException.ThrowIfNull(propertyBuilder);

            propertyBuilder.HasAnnotation(Annotations.EncryptedPropertyAnnotation, true);

            return propertyBuilder;
        }
    }
}

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Common.Persistence;

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

    extension<TProperty>(ComplexTypePropertyBuilder<TProperty> propertyBuilder)
    {
        /// <summary>
        /// Marks a complex type's property to be encrypted at rest, whether it's stored in its owner's columns or
        /// its JSON.
        /// </summary>
        public ComplexTypePropertyBuilder<TProperty> Encrypted()
        {
            ArgumentNullException.ThrowIfNull(propertyBuilder);

            propertyBuilder.HasAnnotation(Annotations.EncryptedPropertyAnnotation, true);

            return propertyBuilder;
        }
    }

    extension<TProperty>(ComplexCollectionTypePropertyBuilder<TProperty> propertyBuilder)
    {
        /// <summary>
        /// Marks a property of a list's values to be encrypted at rest, inside the list's JSON.
        /// </summary>
        public ComplexCollectionTypePropertyBuilder<TProperty> Encrypted()
        {
            ArgumentNullException.ThrowIfNull(propertyBuilder);

            propertyBuilder.HasAnnotation(Annotations.EncryptedPropertyAnnotation, true);

            return propertyBuilder;
        }
    }
}

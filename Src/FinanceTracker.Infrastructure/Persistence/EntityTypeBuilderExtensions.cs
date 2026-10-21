using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence;

public static class EntityTypeBuilderExtensions
{
    /// <summary>
    /// Adds a blind index for an encrypted property: a shadow column holding a keyed hash of its value. Encrypted
    /// values differ every time they're written, so this is what unique indexes and lookups use instead.
    /// </summary>
    /// <returns>The blind index property's name, for use in indexes.</returns>
    public static string HasBlindIndex<TEntity>(this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, string>> encryptedProperty) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(encryptedProperty);

        string source = ((MemberExpression)encryptedProperty.Body).Member.Name;
        string blindIndex = BlindIndexPropertyName(source);

        builder.Property<string>(blindIndex)
            .IsRequired()
            .HasAnnotation(Annotations.BlindIndexSourceAnnotation, source);

        return blindIndex;
    }

    public static string BlindIndexPropertyName(string encryptedProperty) => $"{encryptedProperty}BlindIndex";
}

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence;

public static class EntityTypeBuilderExtensions
{
    extension<TEntity>(EntityTypeBuilder<TEntity> builder) where TEntity : class
    {
        /// <summary>
        /// Adds a blind index for an encrypted property: a shadow column holding a keyed hash of its value. Encrypted
        /// values differ every time they're written, so this is what unique indexes and lookups use instead.
        /// </summary>
        /// <returns>The blind index property's name, for use in indexes.</returns>
        public string HasBlindIndex(Expression<Func<TEntity, string>> encryptedProperty)
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

        /// <summary>
        /// Adds a blind index on the UTC day of an encrypted date, see <see cref="DayBlindIndex"/>, so date ranges can
        /// be looked up without decrypting every row.
        /// </summary>
        /// <returns>The blind index property's name, for use in indexes.</returns>
        public string HasDayBlindIndex(Expression<Func<TEntity, DateTime>> encryptedProperty)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(encryptedProperty);

            string source = ((MemberExpression)encryptedProperty.Body).Member.Name;
            string blindIndex = DayBlindIndexPropertyName(source);

            builder.Property<string>(blindIndex)
                .IsRequired()
                .HasAnnotation(Annotations.DayBlindIndexSourceAnnotation, source);

            return blindIndex;
        }
    }

    public static string BlindIndexPropertyName(string encryptedProperty) => $"{encryptedProperty}BlindIndex";

    public static string DayBlindIndexPropertyName(string encryptedProperty) => $"{encryptedProperty}DayBlindIndex";
}

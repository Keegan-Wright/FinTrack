using System.Reflection;
using FinanceTracker.Infrastructure.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// Sets up column encryption on the EF model.
/// </summary>
public static class ModelBuilderExtensions
{
    extension(ModelBuilder modelBuilder)
    {
        /// <summary>
        /// Gives every property marked <see cref="PropertyBuilderExtensions"/>'s <c>Encrypted</c> a converter that
        /// encrypts it.
        /// </summary>
        /// <param name="encryptionService">Encrypts and decrypts the values.</param>
        public ModelBuilder UseEncryption(IColumnEncryptionService encryptionService)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            ArgumentNullException.ThrowIfNull(encryptionService);

            foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
            {
                var encrptionMarkedProperties = entityType.GetProperties()
                    .Where(x => x.FindAnnotation(Annotations.EncryptedPropertyAnnotation) != null)
                    .ToList();

                foreach (IMutableProperty? markedProperty in encrptionMarkedProperties)
                {
                    markedProperty.SetValueConverter(CreateConverter(markedProperty.ClrType, encryptionService));

                    // With a converter EF compares collections by reference, so changes made in place are never saved
                    if (CreateCollectionComparer(markedProperty.ClrType) is { } collectionComparer)
                    {
                        markedProperty.SetValueComparer(collectionComparer);
                    }
                }
            }

            return modelBuilder;
        }

        /// <summary>
        /// Gives every table of the app's own data an integrity tag column, see <see cref="RowIntegrity"/>. The domain's
        /// entities, not the types they own (stored inside their owner's row), the Identity tables or the framework's.
        /// </summary>
        public ModelBuilder UseRowIntegrity()
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes()
                         .Where(static x => !x.IsOwned() && typeof(Domain.Common.Entity).IsAssignableFrom(x.ClrType))
                         .ToList())
            {
                entityType.AddProperty(RowIntegrity.TagProperty, typeof(string)).IsNullable = true;
                entityType.SetAnnotation(Annotations.RowIntegrityAnnotation, true);
            }

            return modelBuilder;
        }
    }

    /// <summary>
    /// An <see cref="EncryptionConverter{TModel}"/> for the property's type.
    /// </summary>
    /// <param name="modelType">The property's type.</param>
    /// <param name="encryptionService">Encrypts and decrypts the values.</param>
    private static ValueConverter CreateConverter(Type modelType, IColumnEncryptionService encryptionService) =>
        (ValueConverter)Activator.CreateInstance(
            typeof(EncryptionConverter<>).MakeGenericType(modelType), encryptionService)!;

    /// <summary>
    /// Element by element comparer for array properties and collection properties a <see cref="List{T}"/> can stand
    /// in for, null for anything else.
    /// </summary>
    /// <param name="modelType">The property's type.</param>
    private static ValueComparer? CreateCollectionComparer(Type modelType)
    {
        if (modelType.IsArray)
        {
            return (ValueComparer)typeof(ModelBuilderExtensions)
                .GetMethod(nameof(CreateArrayComparer), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(modelType.GetElementType()!)
                .Invoke(null, null)!;
        }

        Type? elementType = modelType.GetInterfaces().Append(modelType)
            .Where(static x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(static x => x.GetGenericArguments()[0])
            .FirstOrDefault();

        if (modelType == typeof(string) || elementType is null ||
            !modelType.IsAssignableFrom(typeof(List<>).MakeGenericType(elementType)))
        {
            return null;
        }

        return (ValueComparer)typeof(ModelBuilderExtensions)
            .GetMethod(nameof(CreateSequenceComparer), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(modelType, elementType)
            .Invoke(null, null)!;
    }

    /// <summary>
    /// Compares two collections element by element.
    /// </summary>
    /// <typeparam name="TCollection">The collection's type.</typeparam>
    /// <typeparam name="TElement">Its elements' type.</typeparam>
    private static ValueComparer<TCollection> CreateSequenceComparer<TCollection, TElement>()
        where TCollection : class, IEnumerable<TElement> =>
        new(
            static (left, right) => left == null ? right == null : right != null && left.SequenceEqual(right),
            static collection => collection == null
                ? 0
                : collection.Aggregate(0, static (hash, element) => HashCode.Combine(hash, element)),
            static collection => collection == null ? null! : (TCollection)(object)collection.ToList());

    /// <summary>
    /// Compares two arrays element by element.
    /// </summary>
    /// <typeparam name="TElement">Its elements' type.</typeparam>
    private static ValueComparer<TElement[]> CreateArrayComparer<TElement>() =>
        new(
            static (left, right) => left == null ? right == null : right != null && left.SequenceEqual(right),
            static array => array == null
                ? 0
                : array.Aggregate(0, static (hash, element) => HashCode.Combine(hash, element)),
            static array => array == null ? null! : array.ToArray());
}

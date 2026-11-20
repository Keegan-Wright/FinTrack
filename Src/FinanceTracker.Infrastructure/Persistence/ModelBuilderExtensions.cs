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
        /// Gives rows an integrity tag column, see <see cref="RowIntegrity"/>. The domain's entities, the types they own
        /// that hold something worth covering (stored in their owner's row or their own table, not JSON), and
        /// Identity's tokens and passkeys, which an attacker could otherwise copy or move to sign in as someone.
        /// </summary>
        public ModelBuilder UseRowIntegrity()
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            List<IMutableEntityType> tagged = modelBuilder.Model.GetEntityTypes()
                .Where(static x => !x.IsOwned() && IsTaggedType(x.ClrType))
                .ToList();

            List<IMutableEntityType> owned = modelBuilder.Model.GetEntityTypes()
                .Where(static x => x.IsOwned() && CoversMoreThanItsKey(x))
                .Where(x => tagged.Contains(Root(x)))
                .ToList();

            foreach (IMutableEntityType entityType in tagged.Concat(owned))
            {
                entityType.AddProperty(RowIntegrity.TagProperty, typeof(string)).IsNullable = true;
                entityType.SetAnnotation(Annotations.RowIntegrityAnnotation, true);
            }

            return modelBuilder;
        }
    }

    /// <summary>
    /// Whether rows of <paramref name="type"/> carry a tag. The domain's entities, and Identity's tokens (the
    /// authenticator key, recovery codes) and passkeys. Not Identity's users, which it writes often and partly outside
    /// EF's tracking.
    /// </summary>
    /// <param name="type">The entity's class.</param>
    private static bool IsTaggedType(Type type) =>
        typeof(Domain.Common.Entity).IsAssignableFrom(type)
        || type == typeof(Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>)
        || type == typeof(Microsoft.AspNetCore.Identity.IdentityUserPasskey<Guid>);

    /// <summary>
    /// Whether an owned type has something to cover beyond its key, which is its owner's, so a tag would add nothing.
    /// </summary>
    /// <param name="entityType">The owned type.</param>
    private static bool CoversMoreThanItsKey(IMutableEntityType entityType) =>
        entityType.GetProperties().Any(static x => !x.IsPrimaryKey()
                                                   && (x.IsForeignKey()
                                                       || x.FindAnnotation(Annotations.EncryptedPropertyAnnotation)
                                                       is not null));

    /// <summary>
    /// The entity at the top of an owned type's chain of owners.
    /// </summary>
    /// <param name="entityType">The owned type.</param>
    private static IMutableEntityType Root(IMutableEntityType entityType)
    {
        IMutableEntityType current = entityType;
        while (current.FindOwnership() is { } ownership)
        {
            current = ownership.PrincipalEntityType;
        }

        return current;
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

using System.Reflection;
using FinanceTracker.Infrastructure.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceTracker.Infrastructure.Persistence;

public static class ModelBuilderExtensions
{
    public static ModelBuilder UseEncryption(this ModelBuilder modelBuilder,
        IColumnEncryptionService encryptionService)
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

    private static ValueConverter CreateConverter(Type modelType, IColumnEncryptionService encryptionService) =>
        (ValueConverter)Activator.CreateInstance(
            typeof(EncryptionConverter<>).MakeGenericType(modelType), encryptionService)!;

    /// <summary>
    /// Element by element comparer for collection properties a <see cref="List{T}"/> can stand in for,
    /// null for anything else.
    /// </summary>
    private static ValueComparer? CreateCollectionComparer(Type modelType)
    {
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

    private static ValueComparer<TCollection> CreateSequenceComparer<TCollection, TElement>()
        where TCollection : class, IEnumerable<TElement> =>
        new(
            static (left, right) => left == null ? right == null : right != null && left.SequenceEqual(right),
            static collection => collection == null
                ? 0
                : collection.Aggregate(0, static (hash, element) => HashCode.Combine(hash, element)),
            static collection => collection == null ? null! : (TCollection)(object)collection.ToList());
}

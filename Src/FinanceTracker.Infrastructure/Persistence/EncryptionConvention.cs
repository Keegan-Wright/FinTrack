using FinanceTracker.Domain.Utility;
using FinanceTracker.Infrastructure.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// Applies an <see cref="EncryptionConverter{TModel}"/> to every mapped property marked with <see cref="EncryptAttribute"/>.
/// Must run after all entity configurations have been applied.
/// </summary>
internal static class EncryptionConvention
{
    extension(ModelBuilder builder)
    {
        public void ApplyEncryption(ISymmetricEncryptionService encryptionService)
        {
            foreach (IMutableEntityType entityType in builder.Model.GetEntityTypes())
            {
                ApplyToType(entityType, encryptionService);
            }
        }
    }

    private static void ApplyToType(IMutableTypeBase type, ISymmetricEncryptionService encryptionService)
    {
        foreach (IMutableProperty property in type.GetDeclaredProperties())
        {
            if (property.PropertyInfo is null || !Attribute.IsDefined(property.PropertyInfo, typeof(EncryptAttribute)))
            {
                continue;
            }

            if (property.IsPrimitiveCollection)
            {
                IMutableElementType elementType = property.GetElementType()!;
                elementType.SetValueConverter(CreateConverter(elementType.ClrType, encryptionService));
            }
            else
            {
                property.SetValueConverter(CreateConverter(property.ClrType, encryptionService));
            }
        }

        foreach (IMutableComplexProperty complexProperty in type.GetDeclaredComplexProperties())
        {
            ApplyToType(complexProperty.ComplexType, encryptionService);
        }
    }

    private static ValueConverter CreateConverter(Type modelType, ISymmetricEncryptionService encryptionService) =>
        (ValueConverter)Activator.CreateInstance(
            typeof(EncryptionConverter<>).MakeGenericType(modelType), encryptionService)!;
}

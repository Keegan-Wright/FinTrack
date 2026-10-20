using FinanceTracker.Infrastructure.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceTracker.Infrastructure.Persistence;

public static class ModelBuilderExtensions
{
    public static ModelBuilder UseEncryption(this ModelBuilder modelBuilder,
        ISymmetricEncryptionService encryptionService)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(encryptionService);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var encrptionMarkedProperties = entityType.GetProperties()
                .Where(x => x.FindAnnotation(Annotations.EncryptedPropertyAnnotation) != null)
                .ToList();

            foreach (var markedProperty in encrptionMarkedProperties)
            {
                markedProperty.SetValueConverter(CreateConverter(markedProperty.ClrType, encryptionService));
            }
        }

        return modelBuilder;
    }

    private static ValueConverter CreateConverter(Type modelType, ISymmetricEncryptionService encryptionService) =>
        (ValueConverter)Activator.CreateInstance(
            typeof(EncryptionConverter<>).MakeGenericType(modelType), encryptionService)!;
}

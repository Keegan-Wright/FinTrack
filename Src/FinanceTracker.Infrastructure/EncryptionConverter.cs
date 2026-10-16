using FinanceTracker.Infrastructure.Encryption;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceTracker.Infrastructure;

/// <summary>
/// Encrypts / Decrypts data based on the <see cref="ISymmetricEncryptionService"/>
/// Used to encrypt data at rest and decrypt on load.
/// </summary>
/// <typeparam name="TModel">The value to encrypt|Decrypt</typeparam>
public class EncryptionConverter<TModel> : ValueConverter<TModel, string>
{
    public EncryptionConverter(ISymmetricEncryptionService symmetricEncryptionService) : base(
        v => symmetricEncryptionService.Encrypt(v),
        v => symmetricEncryptionService.Decrypt<TModel>(v)!)
    {
    }
}

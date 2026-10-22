using FinanceTracker.Infrastructure.Encryption;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceTracker.Infrastructure;

/// <summary>
/// Encrypts / Decrypts data based on the <see cref="IColumnEncryptionService"/>
/// Used to encrypt data at rest and decrypt on load.
/// </summary>
/// <typeparam name="TModel">The value to encrypt|Decrypt</typeparam>
public class EncryptionConverter<TModel> : ValueConverter<TModel, string>
{
    public EncryptionConverter(IColumnEncryptionService columnEncryptionService) : base(
        v => columnEncryptionService.Encrypt(v),
        v => columnEncryptionService.Decrypt<TModel>(v)!)
    {
    }
}

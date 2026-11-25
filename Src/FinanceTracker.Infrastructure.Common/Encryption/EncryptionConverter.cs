using FinanceTracker.Infrastructure.Common.Encryption;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceTracker.Infrastructure.Common.Encryption;

/// <summary>
/// Encrypts / Decrypts data based on the <see cref="IColumnEncryptionService"/>
/// Used to encrypt data at rest and decrypt on load.
/// </summary>
/// <typeparam name="TModel">The value to encrypt|Decrypt</typeparam>
public class EncryptionConverter<TModel> : ValueConverter<TModel, string>
{
    /// <param name="columnEncryptionService">Encrypts and decrypts the values.</param>
    public EncryptionConverter(IColumnEncryptionService columnEncryptionService) : base(
        v => columnEncryptionService.Encrypt(v),
        v => columnEncryptionService.Decrypt<TModel>(v)!)
    {
    }
}

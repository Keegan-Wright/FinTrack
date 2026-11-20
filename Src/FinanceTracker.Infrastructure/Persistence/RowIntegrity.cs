using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using FinanceTracker.Infrastructure.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// What a row's integrity tag covers. The table, the row's key, its foreign keys (its owner's <c>UserId</c> and its
/// parent's id) and its encrypted values, so moving a row to another owner or parent, swapping values between rows,
/// or copying a row under a new id is caught on load. Without the key a tag can't be made for the changed row.
/// </summary>
/// <remarks>
/// Values are covered as the app holds them, decrypted, as EF encrypts them with a fresh nonce only after the tag is
/// worked out. AES-GCM already stops an encrypted value being edited. Fields are written as a name and a value, each
/// length prefixed, in name order. Null and empty values are left out, so a column added later as nullable doesn't
/// break the tags of rows written before it. Renaming a covered property or adding a required one needs the rows
/// tagged again.
/// </remarks>
internal static class RowIntegrity
{
    /// <summary>
    /// The shadow property and column holding the tag.
    /// </summary>
    public const string TagProperty = "IntegrityTag";

    private static readonly ConcurrentDictionary<IReadOnlyEntityType, IReadOnlyList<IProperty>> CoveredCache = new();

    /// <summary>
    /// Whether <paramref name="entityType"/>'s rows carry a tag.
    /// </summary>
    /// <param name="entityType">The entity.</param>
    public static bool IsTagged(IReadOnlyEntityType entityType) =>
        entityType.FindAnnotation(Annotations.RowIntegrityAnnotation) is not null;

    /// <summary>
    /// The properties the tag covers. The key, the foreign keys and the encrypted properties, in name order.
    /// </summary>
    /// <param name="entityType">The entity.</param>
    public static IReadOnlyList<IProperty> Covered(IEntityType entityType) =>
        CoveredCache.GetOrAdd(entityType, static x => ((IEntityType)x).GetProperties()
            .Where(static property => property.IsKey()
                                      || property.IsForeignKey()
                                      || property.FindAnnotation(Annotations.EncryptedPropertyAnnotation) is not null)
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .ToList());

    /// <summary>
    /// The bytes the tag is worked out over for a row of <paramref name="entityType"/>.
    /// </summary>
    /// <param name="entityType">The entity.</param>
    /// <param name="valueOf">Each covered property's value as the app holds it.</param>
    public static byte[] Content(IEntityType entityType, Func<IProperty, object?> valueOf)
    {
        using MemoryStream content = new();
        Write(content, entityType.GetTableName() ?? entityType.Name);
        foreach (IProperty property in Covered(entityType))
        {
            string? value = PlainText.From(valueOf(property));
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            Write(content, property.Name);
            Write(content, value);
        }

        return content.ToArray();
    }

    /// <summary>
    /// Writes <paramref name="text"/> as its UTF-8 length then its bytes, so one field can't run into the next.
    /// </summary>
    /// <param name="content">Where it's written.</param>
    /// <param name="text">The text.</param>
    private static void Write(MemoryStream content, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        content.Write(length);
        content.Write(bytes);
    }
}

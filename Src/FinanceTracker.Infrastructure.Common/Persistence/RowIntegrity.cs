using System.Buffers.Binary;
using System.Collections;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using FinanceTracker.Infrastructure.Common.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FinanceTracker.Infrastructure.Common.Persistence;

/// <summary>
/// What a row's integrity tag covers. The table, the row's key, its foreign keys (its owner's <c>UserId</c> and its
/// parent's id) and its encrypted values, including those in its complex types whether they're stored in its columns
/// or as JSON. So moving a row to another owner or parent, swapping values between rows, or copying a row under a new
/// id is caught on load. Without the key a tag can't be made for the changed row.
/// </summary>
/// <remarks>
/// Values are covered as the app holds them, decrypted, as EF encrypts them with a fresh nonce only after the tag is
/// worked out. AES-GCM already stops an encrypted value being edited. Fields are written as a name and a value, each
/// length prefixed. The row's own values come first in name order, then its complex types' in name order, a list's
/// items in their order. Null and empty values are left out, so a column added later as nullable doesn't break the
/// tags of rows written before it. Renaming a covered property or adding a required one needs the rows tagged again,
/// as does changing what's covered, which is what <see cref="FormatVersion"/> is for.
/// </remarks>
internal static class RowIntegrity
{
    /// <summary>
    /// The shadow property and column holding the tag.
    /// </summary>
    public const string TagProperty = "IntegrityTag";

    /// <summary>
    /// The version of what a tag covers, at the start of every tag. A row tagged in an older version is tagged again
    /// at startup by <see cref="Encryption.EncryptionKeyRotator{TContext}"/>, and refused if one turns up after that. Version 2 covers
    /// complex types.
    /// </summary>
    public const string FormatVersion = "v2";

    /// <summary>
    /// Separates the format version from the rest of the tag.
    /// </summary>
    private const char VersionSeparator = '.';

    private static readonly ConcurrentDictionary<IReadOnlyEntityType, IReadOnlyList<IProperty>> CoveredCache = new();

    private static readonly ConcurrentDictionary<IReadOnlyTypeBase, IReadOnlyList<IProperty>> CoveredOfComplexCache =
        new();

    private static readonly ConcurrentDictionary<IReadOnlyTypeBase, IReadOnlyList<IComplexProperty>>
        CoveredComplexCache = new();

    /// <summary>
    /// Whether <paramref name="entityType"/>'s rows carry a tag.
    /// </summary>
    /// <param name="entityType">The entity.</param>
    public static bool IsTagged(IReadOnlyEntityType entityType) =>
        entityType.FindAnnotation(Annotations.RowIntegrityAnnotation) is not null;

    /// <summary>
    /// What a tag under key <paramref name="keyId"/> at the current format starts with.
    /// </summary>
    /// <param name="keyId">The key's id.</param>
    public static string TagPrefix(int keyId) => $"{FormatVersion}{VersionSeparator}{keyId}:";

    /// <summary>
    /// The tag for <paramref name="content"/> under the current key, at the current format.
    /// </summary>
    /// <param name="encryptionService">Holds the keys.</param>
    /// <param name="content">The row's content, see <see cref="Content"/>.</param>
    public static string Tag(IColumnEncryptionService encryptionService, byte[] content) =>
        $"{FormatVersion}{VersionSeparator}{encryptionService.ComputeRowTag(content)}";

    /// <summary>
    /// Whether <paramref name="tag"/> was made at the current format, so it can be checked.
    /// </summary>
    /// <param name="tag">The tag.</param>
    public static bool IsCurrentFormat(string tag) =>
        tag.StartsWith($"{FormatVersion}{VersionSeparator}", StringComparison.Ordinal);

    /// <summary>
    /// Whether <paramref name="tag"/>, at the current format, matches <paramref name="content"/>.
    /// </summary>
    /// <param name="encryptionService">Holds the keys.</param>
    /// <param name="tag">The tag.</param>
    /// <param name="content">The row's content, see <see cref="Content"/>.</param>
    public static bool IsValid(IColumnEncryptionService encryptionService, string tag, byte[] content) =>
        IsCurrentFormat(tag) && encryptionService.IsRowTagValid(tag[(FormatVersion.Length + 1)..], content);

    /// <summary>
    /// The row's own properties the tag covers. The key, the foreign keys and the encrypted properties, in name
    /// order.
    /// </summary>
    /// <param name="entityType">The entity.</param>
    public static IReadOnlyList<IProperty> Covered(IEntityType entityType) =>
        CoveredCache.GetOrAdd(entityType, static x => ((IEntityType)x).GetProperties()
            .Where(static property => property.IsKey()
                                      || property.IsForeignKey()
                                      || IsEncrypted(property))
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .ToList());

    /// <summary>
    /// The complex properties of <paramref name="type"/> holding encrypted values, at any depth, in name order.
    /// </summary>
    /// <param name="type">The entity or complex type.</param>
    public static IReadOnlyList<IComplexProperty> CoveredComplex(ITypeBase type) =>
        CoveredComplexCache.GetOrAdd(type, static x => ((ITypeBase)x).GetComplexProperties()
            .Where(static complexProperty => HoldsEncrypted(complexProperty.ComplexType))
            .OrderBy(static complexProperty => complexProperty.Name, StringComparer.Ordinal)
            .ToList());

    /// <summary>
    /// Whether some of the values <paramref name="entityType"/>'s tag covers are stored as JSON. EF fills those in
    /// only once the row has been read, after <see cref="RowIntegrityCheckInterceptor"/> has seen it.
    /// </summary>
    /// <param name="entityType">The entity.</param>
    public static bool CoversJson(IEntityType entityType) =>
        CoveredComplex(entityType).Any(static x => x.ComplexType.IsMappedToJson());

    /// <summary>
    /// Whether <paramref name="property"/> is marked to be encrypted.
    /// </summary>
    /// <param name="property">The property.</param>
    public static bool IsEncrypted(IReadOnlyProperty property) =>
        property.FindAnnotation(Annotations.EncryptedPropertyAnnotation) is not null;

    /// <summary>
    /// The bytes the tag is worked out over for a row of <paramref name="entityType"/>.
    /// </summary>
    /// <param name="entityType">The entity.</param>
    /// <param name="valueOf">Each of the row's own covered properties values as the app holds it.</param>
    /// <param name="entity">The row's entity, which its complex types values are read from.</param>
    public static byte[] Content(IEntityType entityType, Func<IProperty, object?> valueOf, object entity)
    {
        using MemoryStream content = new();
        Write(content, FormatVersion);
        Write(content, entityType.GetTableName() ?? entityType.Name);
        foreach (IProperty property in Covered(entityType))
        {
            WriteField(content, property.Name, valueOf(property));
        }

        foreach (IComplexProperty complexProperty in CoveredComplex(entityType))
        {
            WriteComplex(content, complexProperty.Name, complexProperty,
                complexProperty.GetGetter().GetClrValue(entity));
        }


        byte[] copied = content.ToArray();
        CryptographicOperations.ZeroMemory(content.GetBuffer());
        return copied;
    }

    /// <summary>
    /// Whether a complex type holds an encrypted value, in itself or the complex types it holds.
    /// </summary>
    /// <param name="complexType">The complex type.</param>
    private static bool HoldsEncrypted(IComplexType complexType) =>
        complexType.GetProperties().Any(IsEncrypted)
        || complexType.GetComplexProperties().Any(static x => HoldsEncrypted(x.ComplexType));

    /// <summary>
    /// A complex type's encrypted properties, in name order.
    /// </summary>
    /// <param name="complexType">The complex type.</param>
    private static IReadOnlyList<IProperty> CoveredOf(IComplexType complexType) =>
        CoveredOfComplexCache.GetOrAdd(complexType, static x => ((IComplexType)x).GetProperties()
            .Where(IsEncrypted)
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .ToList());

    /// <summary>
    /// Writes a complex property's covered values, each item's in turn for a list, nothing when it's null.
    /// </summary>
    /// <param name="content">Where it's written.</param>
    /// <param name="path">The property's path from the row, e.g. <c>Data</c> or <c>_splits</c>.</param>
    /// <param name="complexProperty">The complex property.</param>
    /// <param name="value">Its value, a list of items for a collection.</param>
    private static void WriteComplex(MemoryStream content, string path, IComplexProperty complexProperty,
        object? value)
    {
        if (value is null)
        {
            return;
        }

        if (!complexProperty.IsCollection)
        {
            WriteComplexValue(content, path, complexProperty.ComplexType, value);
            return;
        }

        int index = 0;
        foreach (object? item in (IEnumerable)value)
        {
            if (item is not null)
            {
                WriteComplexValue(content, $"{path}[{index}]", complexProperty.ComplexType, item);
            }

            index++;
        }
    }

    /// <summary>
    /// Writes one complex value's covered values, and those of the complex types it holds.
    /// </summary>
    /// <param name="content">Where it's written.</param>
    /// <param name="path">The value's path from the row, e.g. <c>_splits[0]</c>.</param>
    /// <param name="complexType">Its complex type.</param>
    /// <param name="value">The value.</param>
    private static void WriteComplexValue(MemoryStream content, string path, IComplexType complexType, object value)
    {
        foreach (IProperty property in CoveredOf(complexType))
        {
            WriteField(content, $"{path}.{property.Name}", property.GetGetter().GetClrValue(value));
        }

        foreach (IComplexProperty nested in CoveredComplex(complexType))
        {
            WriteComplex(content, $"{path}.{nested.Name}", nested, nested.GetGetter().GetClrValue(value));
        }
    }

    /// <summary>
    /// Writes a field's name then its value, nothing when the value is null or empty.
    /// </summary>
    /// <param name="content">Where it's written.</param>
    /// <param name="name">The field's name or path.</param>
    /// <param name="value">Its value as the app holds it.</param>
    private static void WriteField(MemoryStream content, string name, object? value)
    {
        string? text = PlainText.From(value);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Write(content, name);
        Write(content, text);
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
        CryptographicOperations.ZeroMemory(bytes);
    }
}

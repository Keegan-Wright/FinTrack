using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Classifications;

/// <summary>
/// A user defined tag that can be applied to transactions alongside the provider classifications
/// </summary>
public sealed class CustomClassification : AggregateRoot
{
    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private CustomClassification()
    {
    }
#pragma warning restore CS8618

    public CustomClassification(Guid userId, string tag) : base(userId) => Rename(tag);

    public string Tag { get; private set; }

    [MemberNotNull(nameof(Tag))]
    public void Rename(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        Tag = tag;
    }
}

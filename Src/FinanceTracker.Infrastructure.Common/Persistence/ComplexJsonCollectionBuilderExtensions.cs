using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Common.Persistence;

/// <summary>
/// Maps an entity's lists of values to JSON columns as complex collections.
/// </summary>
public static class ComplexJsonCollectionBuilderExtensions
{
    extension<TEntity>(EntityTypeBuilder<TEntity> builder) where TEntity : class
    {
        /// <summary>
        /// Maps a read-only list as an EF complex collection stored in a JSON column of the same name. The entity
        /// keeps the list in a private <see cref="List{T}"/> field named after it (<c>_splits</c> for <c>Splits</c>).
        /// EF needs a list it can write to, so the field is mapped and the property ignored. Queries still read the
        /// list by its property, see <see cref="ComplexJsonCollectionQueryInterceptor"/>.
        /// </summary>
        /// <param name="list">The entity's read-only list.</param>
        /// <param name="configure">Configures the list's values, e.g. which are encrypted.</param>
        public EntityTypeBuilder<TEntity> ComplexJsonCollection<TElement>(
            Expression<Func<TEntity, IReadOnlyCollection<TElement>>> list,
            Action<ComplexCollectionBuilder<TElement>>? configure = null)
            where TElement : notnull
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(list);

            string name = MemberName(list.Body);
            builder.Ignore(name);
            return builder.ComplexCollection<List<TElement>, TElement>(FieldName(name), element =>
            {
                element.ToJson(name);
                configure?.Invoke(element);
            });
        }
    }

    /// <summary>
    /// The private field an entity keeps a list in, <c>_splits</c> for <c>Splits</c>.
    /// </summary>
    /// <param name="listName">The list's property name.</param>
    public static string FieldName(string listName)
    {
        ArgumentException.ThrowIfNullOrEmpty(listName);

        return $"_{char.ToLowerInvariant(listName[0])}{listName[1..]}";
    }

    /// <summary>
    /// The member a property expression reads, through the conversion to the list interface when there is one.
    /// </summary>
    /// <param name="body">The expression's body.</param>
    private static string MemberName(Expression body) => body switch
    {
        MemberExpression member => member.Member.Name,
        UnaryExpression { NodeType: ExpressionType.Convert, Operand: MemberExpression member } => member.Member.Name,
        _ => throw new ArgumentException("Expected a property, e.g. x => x.Splits.", nameof(body))
    };
}

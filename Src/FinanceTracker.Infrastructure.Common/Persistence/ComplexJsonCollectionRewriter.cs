using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FinanceTracker.Infrastructure.Common.Persistence;

/// <summary>
/// Rewrites a query's reads of an entity's read-only list, e.g. <c>x.RuleTags</c>, to the private field EF maps it
/// through, see <see cref="ComplexJsonCollectionBuilderExtensions"/>. EF ignores the property itself, so without this
/// it couldn't translate the read.
/// </summary>
/// <param name="model">The model, which says which lists are mapped through their fields.</param>
internal sealed class ComplexJsonCollectionRewriter(IModel model) : ExpressionVisitor
{
    private static readonly MethodInfo PropertyMethod = typeof(EF).GetMethod(nameof(EF.Property))!;

    /// <inheritdoc/>
    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is null || node.Member is not PropertyInfo ||
            FieldMappedList(node.Expression.Type, node.Member.Name) is not { } list)
        {
            return base.VisitMember(node);
        }

        // As the field, then back to the property's type so the rest of the query still fits
        Expression entity = Visit(node.Expression);
        MethodCallExpression field = Expression.Call(PropertyMethod.MakeGenericMethod(list.ClrType), entity,
            Expression.Constant(list.Name));
        return Expression.Convert(field, node.Type);
    }

    /// <summary>
    /// The complex collection mapped through the field behind <paramref name="propertyName"/> on an entity of
    /// <paramref name="type"/>, null when there isn't one.
    /// </summary>
    /// <param name="type">The type the property is read from.</param>
    /// <param name="propertyName">The property's name.</param>
    private IComplexProperty? FieldMappedList(Type type, string propertyName)
    {
        if (model.FindEntityType(type) is not { } entityType)
        {
            return null;
        }

        IComplexProperty? list =
            entityType.FindComplexProperty(ComplexJsonCollectionBuilderExtensions.FieldName(propertyName));
        return list is { IsCollection: true } ? list : null;
    }
}

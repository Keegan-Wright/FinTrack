using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// Lets queries read an entity's lists by their properties, e.g. <c>x.RuleTags.Any(...)</c>, though EF maps them
/// through private fields, see <see cref="ComplexJsonCollectionRewriter"/>. Runs once per query shape, as EF caches
/// the compiled query.
/// </summary>
internal sealed class ComplexJsonCollectionQueryInterceptor : IQueryExpressionInterceptor
{
    public static ComplexJsonCollectionQueryInterceptor Instance { get; } = new();

    public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData) =>
        eventData.Context is { } context
            ? new ComplexJsonCollectionRewriter(context.Model).Visit(queryExpression)
            : queryExpression;
}

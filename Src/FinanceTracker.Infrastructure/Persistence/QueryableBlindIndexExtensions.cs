using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// Filters on encrypted properties through their blind index, the encrypted columns themselves can't be compared.
/// </summary>
internal static class QueryableBlindIndexExtensions
{
    extension<TEntity>(IQueryable<TEntity> query)
    {
        public IQueryable<TEntity> WhereEncryptedEquals(Expression<Func<TEntity, string>> encryptedProperty, string value,
            FinanceTrackerContext context)
        {
            string hash = context.EncryptionService.ComputeBlindIndex(value);
            (ParameterExpression entity, Expression blindIndex) = BlindIndexAccess(encryptedProperty);

            return query.Where(Expression.Lambda<Func<TEntity, bool>>(
                Expression.Equal(blindIndex, Expression.Constant(hash)), entity));
        }

        public IQueryable<TEntity> WhereEncryptedIn(Expression<Func<TEntity, string>> encryptedProperty,
            IEnumerable<string> values, FinanceTrackerContext context)
        {
            List<string> hashes = values.Select(context.EncryptionService.ComputeBlindIndex).ToList();
            (ParameterExpression entity, Expression blindIndex) = BlindIndexAccess(encryptedProperty);

            Expression contains = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(string)],
                Expression.Constant(hashes), blindIndex);

            return query.Where(Expression.Lambda<Func<TEntity, bool>>(contains, entity));
        }
    }

    /// <summary>
    /// Builds an <see cref="EF.Property{TProperty}"/> access to the encrypted property's blind index column, the
    /// shadow property named by <see cref="EntityTypeBuilderExtensions.BlindIndexPropertyName"/>.
    /// </summary>
    private static (ParameterExpression Entity, Expression BlindIndex) BlindIndexAccess<TEntity>(
        Expression<Func<TEntity, string>> encryptedProperty)
    {
        string source = ((MemberExpression)encryptedProperty.Body).Member.Name;
        ParameterExpression entity = Expression.Parameter(typeof(TEntity), "x");
        Expression blindIndex = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(string)], entity,
            Expression.Constant(EntityTypeBuilderExtensions.BlindIndexPropertyName(source)));

        return (entity, blindIndex);
    }
}

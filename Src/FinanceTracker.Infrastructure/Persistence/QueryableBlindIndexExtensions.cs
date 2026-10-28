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

    extension<TEntity>(IQueryable<TEntity> query)
    {
        /// <summary>
        /// Narrows to rows whose encrypted date falls on a day from <paramref name="from"/> to <paramref name="to"/>,
        /// a day either side. Through its <see cref="DayBlindIndex"/>. Filter the decrypted dates for
        /// the exact range. Left as it is when the range is too long to look up by day.
        /// </summary>
        public IQueryable<TEntity> WhereEncryptedDayBetween(Expression<Func<TEntity, DateTime>> encryptedProperty,
            DateTime from, DateTime to, FinanceTrackerContext context)
        {
            return DayBlindIndex.Values(from, to) is { } days
                ? query.WhereDayIn(encryptedProperty, days, context, included: true)
                : query;
        }

        /// <summary>
        /// <see cref="WhereEncryptedDayBetween"/> for a range that may be open at either end.
        /// </summary>
        public IQueryable<TEntity> WhereEncryptedDayWithin(Expression<Func<TEntity, DateTime>> encryptedProperty,
            DateTime? from, DateTime? to, FinanceTrackerContext context)
        {
            DateTime now = context.TimeProvider.GetUtcNow().UtcDateTime;

            // All time, nothing to narrow
            if (from is null && to is null)
            {
                return query;
            }

            // No first day to list from, so leave out the days after the end instead, see DayBlindIndex.ValuesAfter
            if (from is null)
            {
                List<string> after = DayBlindIndex.ValuesAfter(to!.Value, now);
                return after.Count > 0 ? query.WhereDayIn(encryptedProperty, after, context, included: false) : query;
            }

            // No end, nothing is dated more than FutureDays from now
            DateTime end = to ?? now.AddDays(DayBlindIndex.FutureDays);
            return query.WhereEncryptedDayBetween(encryptedProperty, from.Value, end, context);
        }

        /// <summary>
        /// Rows whose day blind index is, or with <paramref name="included"/> false isn't, one of
        /// <paramref name="days"/>.
        /// </summary>
        private IQueryable<TEntity> WhereDayIn(Expression<Func<TEntity, DateTime>> encryptedProperty,
            List<string> days, FinanceTrackerContext context, bool included)
        {
            List<string> hashes = days.Select(context.EncryptionService.ComputeBlindIndex).ToList();
            string source = ((MemberExpression)encryptedProperty.Body).Member.Name;
            ParameterExpression entity = Expression.Parameter(typeof(TEntity), "x");
            Expression blindIndex = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(string)], entity,
                Expression.Constant(EntityTypeBuilderExtensions.DayBlindIndexPropertyName(source)));

            Expression contains = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(string)],
                Expression.Constant(hashes), blindIndex);

            return query.Where(Expression.Lambda<Func<TEntity, bool>>(
                included ? contains : Expression.Not(contains), entity));
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

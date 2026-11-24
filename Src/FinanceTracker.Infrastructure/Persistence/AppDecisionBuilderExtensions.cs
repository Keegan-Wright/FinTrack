using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// Marks the complex properties holding the app's own decisions about an entity, e.g. a transaction's transfer
/// decision or the tags rules gave it. Changing one doesn't stamp the entity's <c>Updated</c>, which the rules and
/// transfer matching runs read as the bank or the user having changed it, see <see cref="AuditingInterceptor"/>.
/// </summary>
public static class AppDecisionBuilderExtensions
{
    extension<TComplex>(ComplexPropertyBuilder<TComplex> builder) where TComplex : notnull
    {
        /// <summary>
        /// Marks the complex property as a decision the app makes, which doesn't count as the entity changing.
        /// </summary>
        public ComplexPropertyBuilder<TComplex> IsAppDecision()
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.HasPropertyAnnotation(Annotations.AppDecisionAnnotation, true);
            return builder;
        }
    }

    extension<TElement>(ComplexCollectionBuilder<TElement> builder) where TElement : notnull
    {
        /// <summary>
        /// Marks the list as decisions the app makes, which don't count as the entity changing.
        /// </summary>
        public ComplexCollectionBuilder<TElement> IsAppDecision()
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.HasPropertyAnnotation(Annotations.AppDecisionAnnotation, true);
            return builder;
        }
    }
}

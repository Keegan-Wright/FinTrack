using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace FinanceTracker.Infrastructure.Common.Persistence;

#pragma warning disable EF1001 // EF has no public hook for after a row's JSON is filled in.

/// <summary>
/// Runs queries as EF does, then checks the rows waiting in <see cref="PendingRowIntegrityChecks"/> before each result
/// is handed back. EF fills in a row's JSON after every materialization hook has run, so a tag covering JSON can only
/// be checked here, whatever the query's shape. A list, a single row, a stream, <c>Find</c> or raw SQL.
/// </summary>
/// <remarks>
/// Derives from EF's internal query compiler, as no public interceptor runs once a row is complete. Covered by
/// <c>RowIntegrityTests</c>, which would fail if an EF upgrade changed how it's called.
/// </remarks>
internal sealed class RowIntegrityQueryCompiler : QueryCompiler
{
    private static readonly MethodInfo CheckedEnumerableMethod = typeof(RowIntegrityQueryCompiler)
        .GetMethod(nameof(CheckedEnumerable), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo CheckedAsyncEnumerableMethod = typeof(RowIntegrityQueryCompiler)
        .GetMethod(nameof(CheckedAsyncEnumerable), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo CheckedTaskMethod = typeof(RowIntegrityQueryCompiler)
        .GetMethod(nameof(CheckedTask), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// How each kind of result is checked, by its type, so the generic methods are only made once.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Delegate?> CheckersByResultType = new();

    private readonly ICurrentDbContext _currentContext;

    /// <inheritdoc/>
    public RowIntegrityQueryCompiler(IQueryContextFactory queryContextFactory, ICompiledQueryCache compiledQueryCache,
        ICompiledQueryCacheKeyGenerator compiledQueryCacheKeyGenerator, IDatabase database,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger, ICurrentDbContext currentContext,
        IEvaluatableExpressionFilter evaluatableExpressionFilter, IModel model)
        : base(queryContextFactory, compiledQueryCache, compiledQueryCacheKeyGenerator, database, logger,
            currentContext, evaluatableExpressionFilter, model) =>
        _currentContext = currentContext;

    /// <inheritdoc/>
    public override TResult Execute<TResult>(Expression query) =>
        Checked(() => base.Execute<TResult>(query));

    /// <inheritdoc/>
    public override TResult ExecuteAsync<TResult>(Expression query, CancellationToken cancellationToken = default) =>
        Checked(() => base.ExecuteAsync<TResult>(query, cancellationToken));

    /// <inheritdoc/>
    public override Func<QueryContext, TResult> CreateCompiledQuery<TResult>(Expression query)
    {
        Func<QueryContext, TResult> compiled = base.CreateCompiledQuery<TResult>(query);
        return queryContext => Checked(() => compiled(queryContext));
    }

    /// <inheritdoc/>
    public override Func<QueryContext, TResult> CreateCompiledAsyncQuery<TResult>(Expression query)
    {
        Func<QueryContext, TResult> compiled = base.CreateCompiledAsyncQuery<TResult>(query);
        return queryContext => Checked(() => compiled(queryContext));
    }

    /// <summary>
    /// Runs the query, and checks the rows it loads before each result is handed back. A list or stream is checked
    /// row by row as it's read, a task once it's done, and a single result straight away.
    /// </summary>
    /// <param name="run">Runs the query.</param>
    private TResult Checked<TResult>(Func<TResult> run)
    {
        DbContext context = _currentContext.Context;
        if (context is not IEncryptedContext encryptedContext)
        {
            return run();
        }

        PendingRowIntegrityChecks pending = encryptedContext.PendingRowIntegrityChecks;
        TResult result;
        try
        {
            result = run();
        }
        catch
        {
            pending.Clear();
            throw;
        }

        if (CheckerFor(typeof(TResult)) is Func<TResult, DbContext, TResult> checker)
        {
            return checker(result, context);
        }

        pending.VerifyAll(context);
        return result;
    }

    /// <summary>
    /// What wraps a result of <paramref name="resultType"/> so it's checked as it's read, null for a single result.
    /// </summary>
    /// <param name="resultType">The query's result type.</param>
    private static Delegate? CheckerFor(Type resultType) =>
        CheckersByResultType.GetOrAdd(resultType, static type =>
        {
            if (!type.IsGenericType)
            {
                return null;
            }

            Type definition = type.GetGenericTypeDefinition();
            MethodInfo? method = null;
            if (definition == typeof(IEnumerable<>))
            {
                method = CheckedEnumerableMethod;
            }
            else if (definition == typeof(IAsyncEnumerable<>))
            {
                method = CheckedAsyncEnumerableMethod;
            }
            else if (definition == typeof(Task<>))
            {
                method = CheckedTaskMethod;
            }

            if (method is null)
            {
                return null;
            }

            Type delegateType = typeof(Func<,,>).MakeGenericType(type, typeof(DbContext), type);
            return method.MakeGenericMethod(type.GetGenericArguments()[0]).CreateDelegate(delegateType);
        });

    /// <summary>
    /// The rows of <paramref name="source"/>, each checked before it's handed back.
    /// </summary>
    /// <param name="source">The query's rows.</param>
    /// <param name="context">The context running it.</param>
    private static IEnumerable<T> CheckedEnumerable<T>(IEnumerable<T> source, DbContext context)
    {
        PendingRowIntegrityChecks pending = EncryptedContexts.Of(context).PendingRowIntegrityChecks;
        using IEnumerator<T> rows = source.GetEnumerator();
        while (true)
        {
            T current;
            try
            {
                if (!rows.MoveNext())
                {
                    break;
                }

                current = rows.Current;
            }
            catch
            {
                pending.Clear();
                throw;
            }

            pending.VerifyAll(context);
            yield return current;
        }

        pending.VerifyAll(context);
    }

    /// <summary>
    /// The rows of <paramref name="source"/>, each checked before it's handed back.
    /// </summary>
    /// <param name="source">The query's rows.</param>
    /// <param name="context">The context running it.</param>
    private static IAsyncEnumerable<T> CheckedAsyncEnumerable<T>(IAsyncEnumerable<T> source,
        DbContext context) =>
        CheckedAsyncRows(source, context);

    /// <summary>
    /// The rows of <paramref name="source"/>, each checked before it's handed back.
    /// </summary>
    /// <param name="source">The query's rows.</param>
    /// <param name="context">The context running it.</param>
    /// <param name="cancellationToken">Stops reading, given by whoever reads the rows.</param>
    private static async IAsyncEnumerable<T> CheckedAsyncRows<T>(IAsyncEnumerable<T> source,
        DbContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        PendingRowIntegrityChecks pending = EncryptedContexts.Of(context).PendingRowIntegrityChecks;
        await using IAsyncEnumerator<T> rows = source.GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            T current;
            try
            {
                if (!await rows.MoveNextAsync())
                {
                    break;
                }

                current = rows.Current;
            }
            catch
            {
                pending.Clear();
                throw;
            }

            pending.VerifyAll(context);
            yield return current;
        }

        pending.VerifyAll(context);
    }

    /// <summary>
    /// The result of <paramref name="task"/>, checked once it's done.
    /// </summary>
    /// <param name="task">The query's task.</param>
    /// <param name="context">The context running it.</param>
    private static async Task<T> CheckedTask<T>(Task<T> task, DbContext context)
    {
        PendingRowIntegrityChecks pending = EncryptedContexts.Of(context).PendingRowIntegrityChecks;
        T result;
        try
        {
            result = await task;
        }
        catch
        {
            pending.Clear();
            throw;
        }

        pending.VerifyAll(context);
        return result;
    }
}

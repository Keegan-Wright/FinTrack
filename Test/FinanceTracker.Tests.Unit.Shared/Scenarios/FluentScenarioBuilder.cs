namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Chains scenario steps, so a test can await one line of them.
/// </summary>
public static class FluentScenarioBuilder
{
    /// <summary>
    /// Runs the next asynchronous step once <paramref name="source"/> finishes.
    /// </summary>
    /// <typeparam name="TSource">What the step runs on.</typeparam>
    /// <typeparam name="TResult">What the step gives back.</typeparam>
    /// <param name="source">The step before.</param>
    /// <param name="func">The next step.</param>
    public static async Task<TResult>
        Do<TSource, TResult>(this Task<TSource> source, Func<TSource, Task<TResult>> func) => await func(await source);

    /// <summary>
    /// Runs an asynchronous step on <paramref name="source"/>.
    /// </summary>
    /// <typeparam name="TSource">What the step runs on.</typeparam>
    /// <typeparam name="TResult">What the step gives back.</typeparam>
    /// <param name="source">What the step runs on.</param>
    /// <param name="func">The step.</param>
    public static async Task<TResult>
        Do<TSource, TResult>(this TSource source, Func<TSource, Task<TResult>> func) => await func(source);

    /// <summary>
    /// Runs the next step once <paramref name="source"/> finishes.
    /// </summary>
    /// <typeparam name="TSource">What the step runs on.</typeparam>
    /// <typeparam name="TResult">What the step gives back.</typeparam>
    /// <param name="source">The step before.</param>
    /// <param name="func">The next step.</param>
    public static async Task<TResult>
        Do<TSource, TResult>(this Task<TSource> source, Func<TSource, TResult> func) => func(await source);
}

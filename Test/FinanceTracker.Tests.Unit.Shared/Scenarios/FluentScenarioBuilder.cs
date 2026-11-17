namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

/// <summary>
/// Chains scenario steps, so a test can await one line of them.
/// </summary>
public static class FluentScenarioBuilder
{
    extension<TSource>(Task<TSource> source)
    {
        /// <summary>
        /// Runs the next asynchronous step once the step before finishes.
        /// </summary>
        /// <typeparam name="TResult">What the step gives back.</typeparam>
        /// <param name="func">The next step.</param>
        public async Task<TResult> Do<TResult>(Func<TSource, Task<TResult>> func) => await func(await source);

        /// <summary>
        /// Runs the next step once the step before finishes.
        /// </summary>
        /// <typeparam name="TResult">What the step gives back.</typeparam>
        /// <param name="func">The next step.</param>
        public async Task<TResult> Do<TResult>(Func<TSource, TResult> func) => func(await source);
    }

    extension<TSource>(TSource source)
    {
        /// <summary>
        /// Runs an asynchronous step on the scenario.
        /// </summary>
        /// <typeparam name="TResult">What the step gives back.</typeparam>
        /// <param name="func">The step.</param>
        public async Task<TResult> Do<TResult>(Func<TSource, Task<TResult>> func) => await func(source);
    }
}

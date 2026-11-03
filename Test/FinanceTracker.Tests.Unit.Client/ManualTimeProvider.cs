namespace FinanceTracker.Tests.Unit.Client;

/// <summary>
/// A clock whose timers only fire when the test says, for pages that check on something every few seconds.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ManualTimer timer = new(callback, state, this);
        lock (_timers)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    /// <summary>
    /// Fires every timer still running, as if its time had come.
    /// </summary>
    public void Fire()
    {
        List<ManualTimer> running;
        lock (_timers)
        {
            running = [.._timers];
        }

        foreach (ManualTimer timer in running)
        {
            timer.Fire();
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (_timers)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, ManualTimeProvider owner) : ITimer
    {
        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

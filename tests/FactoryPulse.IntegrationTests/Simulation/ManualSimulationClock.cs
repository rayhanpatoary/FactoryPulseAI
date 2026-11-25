namespace FactoryPulse.IntegrationTests.Simulation;

internal sealed class ManualSimulationClock : TimeProvider
{
    private Action? tick;
    private long elapsedTicks;

    internal TaskCompletionSource<TimeSpan> TimerStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource TimerStopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => Interlocked.Read(ref elapsedTicks);

    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        tick = () => callback(state);
        TimerStarted.SetResult(period);
        return new ManualTimer(TimerStopped);
    }

    internal void Advance(TimeSpan interval)
    {
        Interlocked.Add(ref elapsedTicks, interval.Ticks);
        (tick ?? throw new InvalidOperationException("The simulation timer has not started."))();
    }

    private sealed class ManualTimer(TaskCompletionSource stopped) : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();

        public void Dispose() => stopped.TrySetResult();

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

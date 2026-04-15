namespace AkkornStudio.UI.Services.Benchmark;

public interface IBenchmarkDelayScheduler
{
    Task DelayAsync(int milliseconds, CancellationToken cancellationToken);
}


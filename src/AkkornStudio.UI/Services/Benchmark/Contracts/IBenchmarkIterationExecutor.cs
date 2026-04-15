namespace AkkornStudio.UI.Services.Benchmark;

public interface IBenchmarkIterationExecutor
{
    Task<double> ExecuteIterationAsync(CancellationToken cancellationToken);
}


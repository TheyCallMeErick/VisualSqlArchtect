namespace VisualSqlArchitect.UI.Services.Benchmark;

public interface IBenchmarkLatencyProfileSampler
{
    int SampleDelayMs(Random random);
}


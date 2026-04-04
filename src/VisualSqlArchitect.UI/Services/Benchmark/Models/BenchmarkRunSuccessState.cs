namespace VisualSqlArchitect.UI.Services.Benchmark;

public readonly record struct BenchmarkRunSuccessState(
    string Progress,
    string NextRunLabel);


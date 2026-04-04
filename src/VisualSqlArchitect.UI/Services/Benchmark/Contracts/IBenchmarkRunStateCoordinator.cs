namespace VisualSqlArchitect.UI.Services.Benchmark;

public interface IBenchmarkRunStateCoordinator
{
    BenchmarkRunUiState BuildStartState();

    string BuildCancelledMessage();

    string BuildFailureMessage(string reason);

    BenchmarkRunUiState BuildFinishState(string currentProgress);
}


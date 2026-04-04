using System.Collections.ObjectModel;

namespace VisualSqlArchitect.UI.Services.Benchmark;

public interface IBenchmarkResultCoordinator
{
    BenchmarkResultApplicationState ApplySuccess(
        ObservableCollection<BenchmarkRunResult> history,
        BenchmarkRunResult result);
}


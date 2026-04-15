using System.Collections.ObjectModel;

namespace AkkornStudio.UI.Services.Benchmark;

public interface IBenchmarkResultCoordinator
{
    BenchmarkResultApplicationState ApplySuccess(
        ObservableCollection<BenchmarkRunResult> history,
        BenchmarkRunResult result);
}


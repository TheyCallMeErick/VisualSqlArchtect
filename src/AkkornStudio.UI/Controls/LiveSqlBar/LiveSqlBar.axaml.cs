using Avalonia.Controls;
using Avalonia;
using AkkornStudio.UI.Converters;
using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.UI.Controls;

public sealed partial class LiveSqlBar : UserControl
{
    public static readonly StyledProperty<bool> ShowPerformanceToolsProperty =
        AvaloniaProperty.Register<LiveSqlBar, bool>(nameof(ShowPerformanceTools), true);

    public bool ShowPerformanceTools
    {
        get => GetValue(ShowPerformanceToolsProperty);
        set => SetValue(ShowPerformanceToolsProperty, value);
    }

    public LiveSqlBar()
    {
        InitializeComponent();

        Button? copyBtn      = this.FindControl<Button>("CopyBtn");
        Button? formatBtn    = this.FindControl<Button>("FormatBtn");
        Button? benchmarkBtn = this.FindControl<Button>("BenchmarkBtn");
        Button? explainBtn   = this.FindControl<Button>("ExplainBtn");

        if (copyBtn is not null)
            copyBtn.Click += async (_, _) => await CopyToClipboardAsync();
        if (formatBtn is not null)
            formatBtn.Click += (_, _) => (DataContext as LiveSqlBarViewModel)?.FormatSql();
        if (benchmarkBtn is not null)
            benchmarkBtn.Click += (_, _) => (DataContext as LiveSqlBarViewModel)?.OpenBenchmark();
        if (explainBtn is not null)
            explainBtn.Click += (_, _) => (DataContext as LiveSqlBarViewModel)?.OpenExplainPlan();
    }

    private async Task CopyToClipboardAsync()
    {
        if (DataContext is not LiveSqlBarViewModel vm)
            return;
        Avalonia.Input.Platform.IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(string.IsNullOrWhiteSpace(vm.DisplaySql) ? vm.RawSql : vm.DisplaySql);
    }
}

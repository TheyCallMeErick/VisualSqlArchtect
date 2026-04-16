using System.IO;

namespace AkkornStudio.Tests.Unit.Controls.Shell;

public class OutputPreviewModalControlCodeBehindRegressionTests
{
    [Fact]
    public void Constructor_WiresDataContextChangedAndConfiguresEditors()
    {
        string source = ReadControlCodeBehind();

        Assert.Contains("InitializeComponent();", source);
        Assert.Contains("ConfigureSqlEditors();", source);
        Assert.Contains("DataContextChanged += (_, _) => AttachViewModel();", source);
    }

    [Fact]
    public void AttachViewModel_UnsubscribesOldVmAndSubscribesNewVm()
    {
        string source = ReadControlCodeBehind();

        Assert.Contains("if (_vm is not null)", source);
        Assert.Contains("_vm.PropertyChanged -= OnViewModelPropertyChanged;", source);
        Assert.Contains("_vm = DataContext as OutputPreviewModalViewModel;", source);
        Assert.Contains("_vm.PropertyChanged += OnViewModelPropertyChanged;", source);
        Assert.Contains("SyncQuerySqlText();", source);
        Assert.Contains("SyncDdlSqlText();", source);
    }

    [Fact]
    public void PropertyChangedHandler_SynchronizesQueryAndDdlEditors()
    {
        string source = ReadControlCodeBehind();

        Assert.Contains("if (e.PropertyName == nameof(OutputPreviewModalViewModel.QuerySqlText))", source);
        Assert.Contains("SyncQuerySqlText();", source);
        Assert.Contains("if (e.PropertyName == nameof(OutputPreviewModalViewModel.DdlSqlText))", source);
        Assert.Contains("SyncDdlSqlText();", source);
    }

    private static string ReadControlCodeBehind()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "AkkornStudio.UI",
                "Controls",
                "Shell",
                "OutputPreviewModalControl.axaml.cs"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate OutputPreviewModalControl.axaml.cs from test base directory.");
    }
}

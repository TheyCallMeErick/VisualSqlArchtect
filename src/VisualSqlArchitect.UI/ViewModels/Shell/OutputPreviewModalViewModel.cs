using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.ViewModels;

public sealed class OutputPreviewModalViewModel : ViewModelBase
{
    public enum EOutputPreviewMode
    {
        Query,
        Ddl,
    }

    public enum EOutputPreviewTab
    {
        Primary,
        Diagnostics,
    }

    private bool _isVisible;
    private EOutputPreviewMode _mode = EOutputPreviewMode.Query;
    private EOutputPreviewTab _activeTab = EOutputPreviewTab.Primary;
    private DataPreviewViewModel? _queryDataPreview;
    private LiveSqlBarViewModel? _queryLiveSql;
    private AppDiagnosticsViewModel? _diagnostics;
    private string _ddlSqlText = string.Empty;
    private string _ddlProviderLabel = string.Empty;

    public OutputPreviewModalViewModel()
    {
        CloseCommand = new RelayCommand(() => IsVisible = false);
        ShowPrimaryTabCommand = new RelayCommand(() => ActiveTab = EOutputPreviewTab.Primary);
        ShowDiagnosticsTabCommand = new RelayCommand(() => ActiveTab = EOutputPreviewTab.Diagnostics);
    }

    public RelayCommand CloseCommand { get; }
    public RelayCommand ShowPrimaryTabCommand { get; }
    public RelayCommand ShowDiagnosticsTabCommand { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    public EOutputPreviewMode Mode
    {
        get => _mode;
        private set
        {
            if (!Set(ref _mode, value))
                return;

            RaisePropertyChanged(nameof(IsQueryMode));
            RaisePropertyChanged(nameof(IsDdlMode));
            RaisePropertyChanged(nameof(Title));
            RaisePropertyChanged(nameof(PrimaryTabLabel));
            RaisePropertyChanged(nameof(ShowPrimaryContent));
            RaisePropertyChanged(nameof(ShowDiagnosticsContent));
            RaisePropertyChanged(nameof(ShowQueryPrimaryContent));
            RaisePropertyChanged(nameof(ShowDdlPrimaryContent));
        }
    }

    public bool IsQueryMode => Mode == EOutputPreviewMode.Query;
    public bool IsDdlMode => Mode == EOutputPreviewMode.Ddl;

    public EOutputPreviewTab ActiveTab
    {
        get => _activeTab;
        private set
        {
            if (!Set(ref _activeTab, value))
                return;

            RaisePropertyChanged(nameof(ShowPrimaryContent));
            RaisePropertyChanged(nameof(ShowDiagnosticsContent));
            RaisePropertyChanged(nameof(ShowQueryPrimaryContent));
            RaisePropertyChanged(nameof(ShowDdlPrimaryContent));
        }
    }

    public string Title => IsDdlMode ? "SQL DDL Preview" : "Preview";
    public string PrimaryTabLabel => IsDdlMode ? "SQL DDL" : "Preview";

    public bool ShowPrimaryContent => ActiveTab == EOutputPreviewTab.Primary;
    public bool ShowDiagnosticsContent => ActiveTab == EOutputPreviewTab.Diagnostics;
    public bool ShowQueryPrimaryContent => IsQueryMode && ShowPrimaryContent;
    public bool ShowDdlPrimaryContent => IsDdlMode && ShowPrimaryContent;

    public DataPreviewViewModel? QueryDataPreview
    {
        get => _queryDataPreview;
        private set => Set(ref _queryDataPreview, value);
    }

    public LiveSqlBarViewModel? QueryLiveSql
    {
        get => _queryLiveSql;
        private set => Set(ref _queryLiveSql, value);
    }

    public AppDiagnosticsViewModel? Diagnostics
    {
        get => _diagnostics;
        private set => Set(ref _diagnostics, value);
    }

    public string DdlSqlText
    {
        get => _ddlSqlText;
        private set
        {
            if (!Set(ref _ddlSqlText, value))
                return;

            RaisePropertyChanged(nameof(HasDdlSql));
        }
    }

    public string DdlProviderLabel
    {
        get => _ddlProviderLabel;
        private set => Set(ref _ddlProviderLabel, value);
    }

    public bool HasDdlSql => !string.IsNullOrWhiteSpace(DdlSqlText);

    public void OpenForQuery(CanvasViewModel canvas)
    {
        Mode = EOutputPreviewMode.Query;
        QueryDataPreview = canvas.DataPreview;
        QueryLiveSql = canvas.LiveSql;
        Diagnostics = canvas.Diagnostics;
        ActiveTab = EOutputPreviewTab.Primary;
        IsVisible = true;
        Diagnostics.RunChecksCommand.Execute(null);
    }

    public void OpenForDdl(CanvasViewModel canvas, LiveDdlBarViewModel liveDdl, string providerLabel)
    {
        Mode = EOutputPreviewMode.Ddl;
        QueryDataPreview = null;
        QueryLiveSql = null;
        Diagnostics = canvas.Diagnostics;
        DdlSqlText = liveDdl.RawSql;
        DdlProviderLabel = providerLabel;
        ActiveTab = EOutputPreviewTab.Primary;
        IsVisible = true;
        Diagnostics.RunChecksCommand.Execute(null);
    }
}

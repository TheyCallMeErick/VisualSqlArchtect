using System.ComponentModel;
using System.Reflection;
using VisualSqlArchitect.Metadata;
using Avalonia;
using VisualSqlArchitect.UI.Services.Localization;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.Canvas.Strategies;

namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// Coordinates the application shell flow between Start Menu and Canvas work area.
/// </summary>
public sealed class ShellViewModel : ViewModelBase
{
    public enum AppMode
    {
        Query,
        Ddl,
    }

    public enum ESettingsSection
    {
        Appearance,
        LanguageRegion,
        DateTime,
        KeyboardShortcuts,
        Privacy,
        Notification,
        Accessibility,
    }

    private bool _isStartVisible = true;
    private bool _isSettingsVisible;
    private ESettingsSection _selectedSettingsSection = ESettingsSection.Appearance;
    private AppMode _activeMode = AppMode.Query;
    private bool _isViewSubcanvasActive;
    private CanvasViewModel? _canvas;
    private CanvasViewModel? _ddlCanvas;
    private CanvasContext _activeCanvasContext = CanvasContext.Query;
    private PropertyChangedEventHandler? _connectionManagerPropertyChanged;
    private readonly ILocalizationService _localization;
    private PropertyChangedEventHandler? _localizationPropertyChanged;

    public ShellViewModel(CanvasViewModel? canvas = null, ILocalizationService? localization = null)
    {
        _localization = localization ?? LocalizationService.Instance;
        Toasts = canvas?.Toasts ?? new ToastCenterViewModel();
        _canvas = canvas;
        StartMenu = new StartMenuViewModel();
        LeftSidebar = new LeftSidebarViewModel();
        RightSidebar = new RightSidebarViewModel();
        OutputPreview = new OutputPreviewModalViewModel();
        QueryModeCommand = new RelayCommand(() => SetActiveMode(AppMode.Query));
        DdlModeCommand = new RelayCommand(() => SetActiveMode(AppMode.Ddl));
        AttachCanvasObservers(_canvas);
        _localizationPropertyChanged = (_, e) =>
        {
            if (e.PropertyName is "" or "Item[]" or nameof(ILocalizationService.CurrentCulture))
            {
                RaisePropertyChanged(nameof(SettingsSectionTitle));
                RaisePropertyChanged(nameof(SettingsSectionSubtitle));
            }
        };
        _localization.PropertyChanged += _localizationPropertyChanged;
        SyncExtractedPanels();
    }

    public CanvasViewModel? Canvas
    {
        get => _canvas;
        private set
        {
            if (!Set(ref _canvas, value))
                return;

            AttachCanvasObservers(value);
            RaisePropertyChanged(nameof(ActiveCanvas));
            RaisePropertyChanged(nameof(IsConnectionManagerVisible));
            SyncExtractedPanels();
        }
    }

    public StartMenuViewModel StartMenu { get; }

    public QueryTabManagerViewModel QueryTabs { get; } = new();

    public LeftSidebarViewModel LeftSidebar { get; }

    public RightSidebarViewModel RightSidebar { get; }

    public ToastCenterViewModel Toasts { get; }
    public CommandPaletteViewModel CommandPalette { get; private set; } = new();

    public OutputPreviewModalViewModel OutputPreview { get; }

    public RelayCommand QueryModeCommand { get; }

    public RelayCommand DdlModeCommand { get; }

    public bool IsStartVisible
    {
        get => _isStartVisible;
        private set
        {
            if (!Set(ref _isStartVisible, value))
                return;

            RaisePropertyChanged(nameof(IsCanvasVisible));
        }
    }

    public bool IsCanvasVisible => !IsStartVisible;

    public AppMode ActiveMode
    {
        get => _activeMode;
        private set
        {
            if (!Set(ref _activeMode, value))
                return;

            RaisePropertyChanged(nameof(IsQueryModeActive));
            RaisePropertyChanged(nameof(IsDdlModeActive));
        }
    }

    public bool IsQueryModeActive => ActiveMode == AppMode.Query;

    public bool IsDdlModeActive => ActiveMode == AppMode.Ddl;

    public CanvasContext ActiveCanvasContext
    {
        get => _activeCanvasContext;
        private set => Set(ref _activeCanvasContext, value);
    }

    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        private set => Set(ref _isSettingsVisible, value);
    }

    public bool IsConnectionManagerVisible => Canvas?.ConnectionManager.IsVisible == true;

    public CanvasViewModel? ActiveCanvas => ActiveMode switch
    {
        AppMode.Ddl => DdlCanvas,
        _ => Canvas,
    };

    public CanvasViewModel? DdlCanvas
    {
        get => _ddlCanvas;
        private set
        {
            if (!Set(ref _ddlCanvas, value))
                return;

            RaisePropertyChanged(nameof(ActiveCanvas));
            SyncExtractedPanels();
        }
    }

    public string AppVersionLabel => ResolveAppVersionLabel();

    public ESettingsSection SelectedSettingsSection
    {
        get => _selectedSettingsSection;
        private set
        {
            if (!Set(ref _selectedSettingsSection, value))
                return;

            RaisePropertyChanged(nameof(IsAppearanceSectionSelected));
            RaisePropertyChanged(nameof(IsLanguageRegionSectionSelected));
            RaisePropertyChanged(nameof(IsDateTimeSectionSelected));
            RaisePropertyChanged(nameof(IsKeyboardShortcutsSectionSelected));
            RaisePropertyChanged(nameof(IsPrivacySectionSelected));
            RaisePropertyChanged(nameof(IsNotificationSectionSelected));
            RaisePropertyChanged(nameof(IsAccessibilitySectionSelected));
            RaisePropertyChanged(nameof(SettingsSectionTitle));
            RaisePropertyChanged(nameof(SettingsSectionSubtitle));
        }
    }

    public string SettingsSectionTitle => SelectedSettingsSection switch
    {
        ESettingsSection.Appearance => Localize("settings.section.appearance.title", "Themes"),
        ESettingsSection.LanguageRegion => Localize("settings.section.languageRegion.title", "Language & Region"),
        ESettingsSection.DateTime => Localize("settings.section.dateTime.title", "Date & Time"),
        ESettingsSection.KeyboardShortcuts => Localize("settings.section.keyboard.title", "Keyboard Shortcuts"),
        ESettingsSection.Privacy => Localize("settings.section.privacy.title", "Privacy"),
        ESettingsSection.Notification => Localize("settings.section.notification.title", "Notification"),
        ESettingsSection.Accessibility => Localize("settings.section.accessibility.title", "Accessibility"),
        _ => Localize("settings.section.default.title", "Settings"),
    };

    public string SettingsSectionSubtitle => SelectedSettingsSection switch
    {
        ESettingsSection.Appearance => Localize("settings.section.appearance.subtitle", "Choose your style or customize your theme"),
        ESettingsSection.LanguageRegion => Localize("settings.section.languageRegion.subtitle", "Manage language and regional formatting"),
        ESettingsSection.DateTime => Localize("settings.section.wip.subtitle", "Work in progress."),
        ESettingsSection.KeyboardShortcuts => Localize("settings.section.wip.subtitle", "Work in progress."),
        ESettingsSection.Privacy => Localize("settings.section.wip.subtitle", "Work in progress."),
        ESettingsSection.Notification => Localize("settings.section.wip.subtitle", "Work in progress."),
        ESettingsSection.Accessibility => Localize("settings.section.wip.subtitle", "Work in progress."),
        _ => Localize("settings.section.default.subtitle", "Application settings"),
    };

    public bool IsAppearanceSectionSelected => SelectedSettingsSection == ESettingsSection.Appearance;
    public bool IsLanguageRegionSectionSelected => SelectedSettingsSection == ESettingsSection.LanguageRegion;
    public bool IsDateTimeSectionSelected => SelectedSettingsSection == ESettingsSection.DateTime;
    public bool IsKeyboardShortcutsSectionSelected => SelectedSettingsSection == ESettingsSection.KeyboardShortcuts;
    public bool IsPrivacySectionSelected => SelectedSettingsSection == ESettingsSection.Privacy;
    public bool IsNotificationSectionSelected => SelectedSettingsSection == ESettingsSection.Notification;
    public bool IsAccessibilitySectionSelected => SelectedSettingsSection == ESettingsSection.Accessibility;

    public CanvasViewModel EnsureCanvas(Func<bool>? isDdlModeActiveResolver = null, Action<TableMetadata, Point>? importDdlTableAction = null)
    {
        if (Canvas is null)
            Canvas = new CanvasViewModel(
                nodeManager: null,
                pinManager: null,
                selectionManager: null,
                localizationService: null,
                domainStrategy: new QueryDomainStrategy(isDdlModeActiveResolver, importDdlTableAction),
                toastCenter: Toasts);

        return Canvas;
    }

    public CanvasViewModel EnsureDdlCanvas()
    {
        if (DdlCanvas is null)
            DdlCanvas = new CanvasViewModel(
                nodeManager: null,
                pinManager: null,
                selectionManager: null,
                localizationService: null,
                domainStrategy: new DdlDomainStrategy(),
                toastCenter: Toasts);

        return DdlCanvas;
    }

    public void SetActiveMode(AppMode mode)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported app mode.");

        if (mode == AppMode.Ddl)
            EnsureDdlCanvas();

        if (mode != AppMode.Ddl)
            _isViewSubcanvasActive = false;

        ActiveMode = mode;
        RaisePropertyChanged(nameof(ActiveCanvas));
        UpdateActiveCanvasContext();
        SyncExtractedPanels();
    }

    public void SetViewSubcanvasActive(bool isActive)
    {
        bool coerced = ActiveMode == AppMode.Ddl && isActive;

        if (!Set(ref _isViewSubcanvasActive, coerced))
            return;

        UpdateActiveCanvasContext();
        SyncExtractedPanels();
    }

    public void EnterCanvas()
    {
        EnsureCanvas();
        IsStartVisible = false;
    }

    public void ReturnToStart() => IsStartVisible = true;

    public void OpenSettings() => IsSettingsVisible = true;

    public void CloseSettings() => IsSettingsVisible = false;

    public void SelectSettingsSection(ESettingsSection section) => SelectedSettingsSection = section;

    public void SetCommandPalette(CommandPaletteViewModel viewModel)
    {
        CommandPalette = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        RaisePropertyChanged(nameof(CommandPalette));
    }

    private static string ResolveAppVersionLabel()
    {
        Assembly asm = typeof(ShellViewModel).Assembly;

        string? informational = asm
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            string clean = informational.Split('+')[0].Trim();
            if (!string.IsNullOrWhiteSpace(clean))
                return clean;
        }

        string? fileVersion = asm
            .GetCustomAttribute<AssemblyFileVersionAttribute>()
            ?.Version;
        if (!string.IsNullOrWhiteSpace(fileVersion))
            return fileVersion;

        return asm.GetName().Version?.ToString() ?? "dev";
    }

    private void AttachCanvasObservers(CanvasViewModel? canvas)
    {
        if (_connectionManagerPropertyChanged is not null && _canvas is not null)
            _canvas.ConnectionManager.PropertyChanged -= _connectionManagerPropertyChanged;

        if (canvas is null)
            return;

        _connectionManagerPropertyChanged = (_, e) =>
        {
            if (e.PropertyName == nameof(ConnectionManagerViewModel.IsVisible))
                RaisePropertyChanged(nameof(IsConnectionManagerVisible));
        };

        canvas.ConnectionManager.PropertyChanged += _connectionManagerPropertyChanged;
    }

    private void SyncExtractedPanels()
    {
        LeftSidebar.BindQuerySidebar(Canvas?.Sidebar);
        RightSidebar.BindPropertyPanel(Canvas?.PropertyPanel);

        bool showQueryScaffold = ActiveMode == AppMode.Query && Canvas is not null;
        LeftSidebar.SyncVisibility(showQueryScaffold);
        RightSidebar.SyncVisibility(showQueryScaffold);
    }

    private void UpdateActiveCanvasContext()
    {
        ActiveCanvasContext = ActiveMode switch
        {
            AppMode.Ddl when _isViewSubcanvasActive => CanvasContext.ViewSubcanvas,
            AppMode.Ddl => CanvasContext.Ddl,
            _ => CanvasContext.Query,
        };
    }

    private string Localize(string key, string fallback)
    {
        string value = _localization[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}

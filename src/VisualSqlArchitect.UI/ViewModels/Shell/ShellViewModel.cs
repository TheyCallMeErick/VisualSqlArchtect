using System.ComponentModel;
using System.Reflection;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// Coordinates the application shell flow between Start Menu and Canvas work area.
/// </summary>
public sealed class ShellViewModel : ViewModelBase
{
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
    private CanvasViewModel? _canvas;
    private PropertyChangedEventHandler? _connectionManagerPropertyChanged;

    public ShellViewModel(CanvasViewModel? canvas = null)
    {
        _canvas = canvas;
        StartMenu = new StartMenuViewModel();
        AttachCanvasObservers(_canvas);
    }

    public CanvasViewModel? Canvas
    {
        get => _canvas;
        private set
        {
            if (!Set(ref _canvas, value))
                return;

            AttachCanvasObservers(value);
            RaisePropertyChanged(nameof(IsConnectionManagerVisible));
        }
    }

    public StartMenuViewModel StartMenu { get; }

    public QueryTabManagerViewModel QueryTabs { get; } = new();

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

    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        private set => Set(ref _isSettingsVisible, value);
    }

    public bool IsConnectionManagerVisible => Canvas?.ConnectionManager.IsVisible == true;

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
        ESettingsSection.Appearance => "Themes",
        ESettingsSection.LanguageRegion => "Language & Region",
        ESettingsSection.DateTime => "Date & Time",
        ESettingsSection.KeyboardShortcuts => "Keyboard Shortcuts",
        ESettingsSection.Privacy => "Privacy",
        ESettingsSection.Notification => "Notification",
        ESettingsSection.Accessibility => "Accessibility",
        _ => "Settings",
    };

    public string SettingsSectionSubtitle => SelectedSettingsSection switch
    {
        ESettingsSection.Appearance => "Choose your style or customize your theme",
        ESettingsSection.LanguageRegion => "Manage language and regional formatting",
        ESettingsSection.DateTime => "Trabalho em progresso.",
        ESettingsSection.KeyboardShortcuts => "Trabalho em progresso.",
        ESettingsSection.Privacy => "Trabalho em progresso.",
        ESettingsSection.Notification => "Trabalho em progresso.",
        ESettingsSection.Accessibility => "Trabalho em progresso.",
        _ => "Application settings",
    };

    public bool IsAppearanceSectionSelected => SelectedSettingsSection == ESettingsSection.Appearance;
    public bool IsLanguageRegionSectionSelected => SelectedSettingsSection == ESettingsSection.LanguageRegion;
    public bool IsDateTimeSectionSelected => SelectedSettingsSection == ESettingsSection.DateTime;
    public bool IsKeyboardShortcutsSectionSelected => SelectedSettingsSection == ESettingsSection.KeyboardShortcuts;
    public bool IsPrivacySectionSelected => SelectedSettingsSection == ESettingsSection.Privacy;
    public bool IsNotificationSectionSelected => SelectedSettingsSection == ESettingsSection.Notification;
    public bool IsAccessibilitySectionSelected => SelectedSettingsSection == ESettingsSection.Accessibility;

    public CanvasViewModel EnsureCanvas()
    {
        if (Canvas is null)
            Canvas = new CanvasViewModel();

        return Canvas;
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
}

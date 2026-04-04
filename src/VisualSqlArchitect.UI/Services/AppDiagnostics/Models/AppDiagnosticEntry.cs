using Material.Icons;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Services.AppDiagnostics.Models;

public sealed class AppDiagnosticEntry : ViewModelBase
{
    private EDiagnosticStatus _status;
    private string _details = "";
    private DateTime _lastCheckAt = DateTime.MinValue;

    public string Name { get; init; } = "";
    public string Recommendation { get; init; } = "";

    public EDiagnosticStatus Status
    {
        get => _status;
        set
        {
            Set(ref _status, value);
            RaisePropertyChanged(nameof(StatusIcon));
            RaisePropertyChanged(nameof(StatusColor));
        }
    }

    public string Details
    {
        get => _details;
        set => Set(ref _details, value);
    }

    public DateTime LastCheckAt
    {
        get => _lastCheckAt;
        set
        {
            Set(ref _lastCheckAt, value);
            RaisePropertyChanged(nameof(LastCheckLabel));
        }
    }

    public string StatusIcon =>
        Status switch
        {
            EDiagnosticStatus.Ok => "✓",
            EDiagnosticStatus.Warning => "⚠",
            EDiagnosticStatus.Error => "✕",
            _ => "?",
        };

    public MaterialIconKind StatusIconKind =>
        Status switch
        {
            EDiagnosticStatus.Ok => MaterialIconKind.CheckCircle,
            EDiagnosticStatus.Warning => MaterialIconKind.Alert,
            EDiagnosticStatus.Error => MaterialIconKind.CloseCircle,
            _ => MaterialIconKind.HelpCircle,
        };

    public string StatusColor =>
        Status switch
        {
            EDiagnosticStatus.Ok => "#4ADE80",
            EDiagnosticStatus.Warning => "#FBBF24",
            EDiagnosticStatus.Error => "#EF4444",
            _ => "#4A5568",
        };

    public string LastCheckLabel =>
        _lastCheckAt == DateTime.MinValue ? "—" : _lastCheckAt.ToString("HH:mm:ss");
}

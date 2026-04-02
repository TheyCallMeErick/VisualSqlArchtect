namespace VisualSqlArchitect.UI.ViewModels;

public sealed class ToastCenterViewModel : ViewModelBase
{
    private bool _isVisible;
    private bool _isDetailsOpen;
    private string _message = string.Empty;
    private string? _details;
    private EToastSeverity _severity;
    private int _version;
    private CancellationTokenSource? _autoHideCts;

    public bool IsVisible
    {
        get => _isVisible;
        private set => Set(ref _isVisible, value);
    }

    public bool IsDetailsOpen
    {
        get => _isDetailsOpen;
        private set => Set(ref _isDetailsOpen, value);
    }

    public string Message
    {
        get => _message;
        private set => Set(ref _message, value);
    }

    public string? Details
    {
        get => _details;
        private set
        {
            Set(ref _details, value);
            RaisePropertyChanged(nameof(HasDetails));
        }
    }

    public bool HasDetails => !string.IsNullOrWhiteSpace(_details);

    public EToastSeverity Severity
    {
        get => _severity;
        private set
        {
            Set(ref _severity, value);
            RaisePropertyChanged(nameof(SeverityLabel));
            RaisePropertyChanged(nameof(SeverityIcon));
            RaisePropertyChanged(nameof(AccentColor));
            RaisePropertyChanged(nameof(DetailsTitle));
        }
    }

    public string SeverityLabel => _severity switch
    {
        EToastSeverity.Success => "Success",
        EToastSeverity.Warning => "Warning",
        _ => "Error",
    };

    public string SeverityIcon => _severity switch
    {
        EToastSeverity.Success => "✔",
        EToastSeverity.Warning => "⚠",
        _ => "✕",
    };

    public string AccentColor => _severity switch
    {
        EToastSeverity.Success => "#22C55E",
        EToastSeverity.Warning => "#F59E0B",
        _ => "#EF4444",
    };

    public string DetailsTitle => _severity switch
    {
        EToastSeverity.Success => "Success Details",
        EToastSeverity.Warning => "Warning Details",
        _ => "Error Details",
    };

    public RelayCommand DismissCommand { get; }
    public RelayCommand ShowDetailsCommand { get; }
    public RelayCommand CloseDetailsCommand { get; }

    public ToastCenterViewModel()
    {
        DismissCommand = new RelayCommand(Dismiss);
        ShowDetailsCommand = new RelayCommand(OpenDetails, () => HasDetails);
        CloseDetailsCommand = new RelayCommand(() => IsDetailsOpen = false);
    }

    public void ShowSuccess(string message, string? details = null) =>
        Show(EToastSeverity.Success, message, details);

    public void ShowError(string message, string? details = null) =>
        Show(EToastSeverity.Error, message, details);

    public void ShowWarning(string message, string? details = null) =>
        Show(EToastSeverity.Warning, message, details);

    private void Show(EToastSeverity severity, string message, string? details)
    {
        _version++;
        CancelAutoHide();

        Severity = severity;
        Message = message;
        Details = details;
        IsDetailsOpen = false;
        IsVisible = true;

        ShowDetailsCommand.NotifyCanExecuteChanged();
        ScheduleAutoHide(_version);
    }

    private void Dismiss()
    {
        CancelAutoHide();
        IsVisible = false;
    }

    private void OpenDetails()
    {
        if (!HasDetails)
            return;

        IsDetailsOpen = true;
    }

    private void ScheduleAutoHide(int version)
    {
        _autoHideCts = new CancellationTokenSource();
        CancellationToken token = _autoHideCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(6), token);
                if (token.IsCancellationRequested || version != _version)
                    return;

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (version == _version && !IsDetailsOpen)
                        IsVisible = false;
                });
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    private void CancelAutoHide()
    {
        _autoHideCts?.Cancel();
        _autoHideCts?.Dispose();
        _autoHideCts = null;
    }
}

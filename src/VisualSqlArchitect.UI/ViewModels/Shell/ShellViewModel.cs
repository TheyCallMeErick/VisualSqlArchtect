using System.ComponentModel;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.ViewModels;

/// <summary>
/// Coordinates the application shell flow between Start Menu and Canvas work area.
/// </summary>
public sealed class ShellViewModel : ViewModelBase
{
    private bool _isStartVisible = true;
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

    public bool IsConnectionManagerVisible => Canvas?.ConnectionManager.IsVisible == true;

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

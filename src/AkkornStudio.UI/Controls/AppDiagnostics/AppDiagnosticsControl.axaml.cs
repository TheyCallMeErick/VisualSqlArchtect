using Avalonia.Controls;
using Avalonia.Input;
using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.UI.Controls;

public sealed partial class AppDiagnosticsControl : UserControl
{
    public AppDiagnosticsControl()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && DataContext is AppDiagnosticsViewModel vm)
        {
            vm.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }
}

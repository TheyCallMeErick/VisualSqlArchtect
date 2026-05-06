using Avalonia.Controls;
using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.UI.Controls.Shell;

public partial class DdlSchemaCompareWorkspaceControl : UserControl
{
    public DdlSchemaCompareWorkspaceControl()
    {
        InitializeComponent();
    }

    private async void CopySqlButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (DataContext is not DdlSchemaCompareWorkspaceViewModel vm || string.IsNullOrWhiteSpace(vm.GeneratedSql))
            return;

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
            return;

        await topLevel.Clipboard.SetTextAsync(vm.GeneratedSql);
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Controls.SqlEditor;

public partial class SqlEditorSchemaBrowserControl : UserControl
{
    public SqlEditorSchemaBrowserControl()
    {
        InitializeComponent();
    }

    private void ClearSchemaFilterButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SqlEditorViewModel vm)
            return;

        vm.SchemaSearchText = string.Empty;
    }

    private void InsertTableButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SqlEditorViewModel vm)
            return;

        if (sender is not Button button || button.Tag is not string fullName || string.IsNullOrWhiteSpace(fullName))
            return;

        vm.AppendTextToEditor(fullName);
    }

    private void InsertColumnButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SqlEditorViewModel vm)
            return;

        if (sender is not Button button || button.Tag is not string columnName || string.IsNullOrWhiteSpace(columnName))
            return;

        vm.AppendTextToEditor(columnName);
    }
}

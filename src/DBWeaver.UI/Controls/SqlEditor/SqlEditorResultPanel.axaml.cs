using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Controls.SqlEditor;

public partial class SqlEditorResultPanel : UserControl
{
    private SqlEditorViewModel? _viewModel;
    private object?[]? _lastSelectedRow;
    private int _lastSelectedColumnIndex = -1;

    public SqlEditorResultPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ResultGrid.KeyDown += OnResultGridKeyDown;
        ResultGrid.Sorting += OnResultGridSorting;
        ConfigureGridContextMenu();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as SqlEditorViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            RefreshGrid(_viewModel.ResultRowsView);
        }
        else
        {
            RefreshGrid(null);
            RowsCounterText.Text = "0 linhas";
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SqlEditorViewModel.ResultRowsView)
            or nameof(SqlEditorViewModel.SelectedResultTabIndex)
            or nameof(SqlEditorViewModel.ResultTabs)
            or nameof(SqlEditorViewModel.ResultGridFilterText)
            or nameof(SqlEditorViewModel.ResultGridSortColumn)
            or nameof(SqlEditorViewModel.ResultGridSortAscending))
        {
            RefreshGrid(_viewModel?.ResultRowsView);
        }
    }

    private void RefreshGrid(DataView? rowsView)
    {
        ResultGrid.Columns.Clear();
        ResultGrid.ItemsSource = null;
        _lastSelectedRow = null;
        _lastSelectedColumnIndex = -1;

        if (rowsView is null)
        {
            RowsCounterText.Text = "0 linhas";
            return;
        }

        DataTable? table = rowsView.Table;
        if (table is null || table.Columns.Count == 0)
        {
            RowsCounterText.Text = "0 linhas";
            return;
        }

        for (int columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
        {
            int capturedColumnIndex = columnIndex;
            string header = table.Columns[columnIndex].ColumnName;

            if (_viewModel?.IsResultColumnHidden(header) == true)
                continue;

            ResultGrid.Columns.Add(new DataGridTemplateColumn
            {
                Header = header,
                IsReadOnly = true,
                CanUserSort = true,
                CellTemplate = new FuncDataTemplate<object?[]>((row, _) =>
                {
                    var textBlock = new TextBlock
                    {
                        Text = FormatCellValue(row, capturedColumnIndex),
                        Padding = new Avalonia.Thickness(12, 6),
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    };

                    textBlock.PointerPressed += (_, _) => CaptureGridCellContext(row, capturedColumnIndex);

                    var copyCellItem = new MenuItem { Header = "Copy Cell" };
                    copyCellItem.Click += async (_, _) =>
                    {
                        CaptureGridCellContext(row, capturedColumnIndex);
                        await CopyTextToClipboardAsync(FormatCellValue(row, capturedColumnIndex));
                    };

                    var copyRowItem = new MenuItem { Header = "Copy Row" };
                    copyRowItem.Click += async (_, _) =>
                    {
                        CaptureGridCellContext(row, capturedColumnIndex);
                        await CopyTextToClipboardAsync(BuildRowClipboardText(row));
                    };

                    var hideColumnItem = new MenuItem { Header = "Hide Column" };
                    hideColumnItem.Click += (_, _) =>
                    {
                        _viewModel?.HideResultColumn(header);
                    };

                    textBlock.ContextMenu = new ContextMenu
                    {
                        ItemsSource = new object[] { copyCellItem, copyRowItem, hideColumnItem },
                    };

                    return textBlock;
                }),
            });
        }

        ResultGrid.FrozenColumnCount = ResultGrid.Columns.Count > 0 ? 1 : 0;

        var rows = new List<object?[]>();
        foreach (object? rowItem in rowsView)
        {
            if (rowItem is not DataRowView rowView)
                continue;

            var values = new object?[table.Columns.Count];
            for (int i = 0; i < table.Columns.Count; i++)
                values[i] = rowView.Row.IsNull(i) ? null : rowView.Row[i];

            rows.Add(values);
        }

        rows = ApplyFilter(rows, table);
        rows = ApplySort(rows, table);

        int totalRows = table.Rows.Count;
        RowsCounterText.Text = rows.Count == totalRows
            ? string.Format(CultureInfo.InvariantCulture, "{0} linhas", totalRows)
            : string.Format(CultureInfo.InvariantCulture, "{0} de {1} linhas", rows.Count, totalRows);

        var observableRows = new ObservableCollection<object?[]>(rows);

        ResultGrid.ItemsSource = observableRows;
    }

    private void ConfigureGridContextMenu()
    {
        var showAllColumnsItem = new MenuItem { Header = "Show All Columns" };
        showAllColumnsItem.Click += (_, _) => _viewModel?.ShowAllResultColumns();

        ResultGrid.ContextMenu = new ContextMenu
        {
            ItemsSource = new object[] { showAllColumnsItem },
        };
    }

    private void ShowAllColumnsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        _viewModel.ShowAllResultColumns();
        RefreshGrid(_viewModel.ResultRowsView);
    }

    private void ClearResultFilterButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        if (string.IsNullOrWhiteSpace(_viewModel.ResultGridFilterText))
            return;

        _viewModel.ResultGridFilterText = string.Empty;
        RefreshGrid(_viewModel.ResultRowsView);
    }

    private void UndoHiddenColumnButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        if (!_viewModel.UndoLastHiddenResultColumn())
            return;

        RefreshGrid(_viewModel.ResultRowsView);
    }

    private void CaptureGridCellContext(object?[]? row, int columnIndex)
    {
        _lastSelectedRow = row;
        _lastSelectedColumnIndex = columnIndex;
    }

    private async void OnResultGridKeyDown(object? sender, KeyEventArgs e)
    {
        bool isCopy = e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (!isCopy)
            return;

        if (_lastSelectedRow is null)
            return;

        bool copyRow = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        string text = copyRow
            ? BuildRowClipboardText(_lastSelectedRow)
            : FormatCellValue(_lastSelectedRow, _lastSelectedColumnIndex);

        await CopyTextToClipboardAsync(text);
        e.Handled = true;
    }

    private string BuildRowClipboardText(object?[]? row)
    {
        if (row is null || row.Length == 0)
            return string.Empty;

        return string.Join('\t', row.Select(value => value?.ToString() ?? string.Empty));
    }

    private async Task CopyTextToClipboardAsync(string text)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
            return;

        await topLevel.Clipboard.SetTextAsync(text ?? string.Empty);
    }

    private List<object?[]> ApplyFilter(List<object?[]> rows, DataTable table)
    {
        if (_viewModel is null || rows.Count == 0)
            return rows;

        string filter = _viewModel.ResultGridFilterText;
        if (string.IsNullOrWhiteSpace(filter))
            return rows;

        string needle = filter.Trim();
        return rows.Where(row => RowMatchesFilter(row, table, needle)).ToList();
    }

    private List<object?[]> ApplySort(List<object?[]> rows, DataTable table)
    {
        if (_viewModel is null || rows.Count == 0)
            return rows;

        string? sortColumn = _viewModel.ResultGridSortColumn;
        if (string.IsNullOrWhiteSpace(sortColumn))
            return rows;

        int sortIndex = table.Columns.IndexOf(sortColumn);
        if (sortIndex < 0)
            return rows;

        return _viewModel.ResultGridSortAscending
            ? rows.OrderBy(row => ToSortKey(row, sortIndex), StringComparer.OrdinalIgnoreCase).ToList()
            : rows.OrderByDescending(row => ToSortKey(row, sortIndex), StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool RowMatchesFilter(object?[] row, DataTable table, string needle)
    {
        for (int i = 0; i < table.Columns.Count; i++)
        {
            if (FormatCellValue(row, i).Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string ToSortKey(object?[] row, int index)
    {
        string value = FormatCellValue(row, index);
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double numeric))
            return numeric.ToString("00000000000000000000.000000", CultureInfo.InvariantCulture);

        return value;
    }

    private void OnResultGridSorting(object? sender, DataGridColumnEventArgs e)
    {
        if (_viewModel is null)
            return;

        string column = e.Column.Header?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(column))
            return;

        bool ascending = true;
        bool clearSorting = false;
        if (string.Equals(_viewModel.ResultGridSortColumn, column, StringComparison.Ordinal))
        {
            if (_viewModel.ResultGridSortAscending)
            {
                ascending = false;
            }
            else
            {
                clearSorting = true;
            }
        }

        _viewModel.SetResultGridSort(clearSorting ? null : column, ascending);
        RefreshGrid(_viewModel.ResultRowsView);
        e.Handled = true;
    }

    private static string FormatCellValue(object?[]? row, int index)
    {
        if (row is null || index < 0 || index >= row.Length || row[index] is null || row[index] is DBNull)
            return string.Empty;

        return row[index]?.ToString() ?? string.Empty;
    }
}


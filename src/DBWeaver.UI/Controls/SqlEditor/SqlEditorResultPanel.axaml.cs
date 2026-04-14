using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Material.Icons;
using Material.Icons.Avalonia;
using DBWeaver.UI.Services.SqlEditor;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.Services.SqlEditor.Reports;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Controls.SqlEditor;

public partial class SqlEditorResultPanel : UserControl
{
    private SqlEditorViewModel? _viewModel;
    private object?[]? _lastSelectedRow;
    private int _lastSelectedColumnIndex = -1;
    private IBrush? _nullCellForegroundBrush;
    private readonly SqlEditorReportExportService _reportExportService = new();
    private SqlInlineEditEligibility _inlineEditEligibility = SqlInlineEditEligibility.NotEligible;
    private readonly Dictionary<string, int> _currentColumnIndexMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<object?[], PendingInlineEdit> _pendingInlineEdits = new(ObjectArrayReferenceComparer.Instance);

    public SqlEditorResultPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ResultGrid.KeyDown += OnResultGridKeyDown;
        ResultGrid.Sorting += OnResultGridSorting;
        ResultGrid.CellEditEnding += OnResultGridCellEditEnding;
        ResultGrid.CellEditEnded += OnResultGridCellEditEnded;
        ResultGrid.ColumnReordered += OnResultGridColumnReordered;
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
            or nameof(SqlEditorViewModel.ResultGridSortAscending)
            or nameof(SqlEditorViewModel.SelectedOutputPane))
        {
            RefreshGrid(_viewModel?.ResultRowsView);
        }

        if (e.PropertyName is nameof(SqlEditorViewModel.OutputMessages)
            or nameof(SqlEditorViewModel.SelectedOutputPane))
        {
            UpdateCounters();
        }
    }

    private void RefreshGrid(DataView? rowsView)
    {
        ResultGrid.Columns.Clear();
        ResultGrid.ItemsSource = null;
        ResultGrid.IsReadOnly = true;
        _inlineEditEligibility = SqlInlineEditEligibility.NotEligible;
        _currentColumnIndexMap.Clear();
        _pendingInlineEdits.Clear();
        _lastSelectedRow = null;
        _lastSelectedColumnIndex = -1;

        if (rowsView is null)
        {
            RowsCounterText.Text = L("sqlEditor.results.rows.countZero", "0 linhas");
            return;
        }

        DataTable? table = rowsView.Table;
        if (table is null || table.Columns.Count == 0)
        {
            RowsCounterText.Text = L("sqlEditor.results.rows.countZero", "0 linhas");
            return;
        }

        _inlineEditEligibility = _viewModel?.EvaluateInlineEditEligibility(table) ?? SqlInlineEditEligibility.NotEligible;
        ResultGrid.IsReadOnly = !_inlineEditEligibility.IsEligible;

        IReadOnlyList<DataColumn> displayColumns = BuildDisplayColumns(table);
        for (int displayIndex = 0; displayIndex < displayColumns.Count; displayIndex++)
        {
            DataColumn dataColumn = displayColumns[displayIndex];
            int capturedColumnIndex = table.Columns.IndexOf(dataColumn);
            string columnName = dataColumn.ColumnName;
            string columnTypeLabel = SqlEditorResultCellContentFormatter.GetColumnTypeLabel(dataColumn);
            SqlEditorSchemaColumnItem? schemaColumn = _viewModel?.ResolveResultSchemaColumn(columnName);
            _currentColumnIndexMap[columnName] = capturedColumnIndex;

            if (_viewModel?.IsResultColumnHidden(columnName) == true)
                continue;

            ResultGrid.Columns.Add(new DataGridTemplateColumn
            {
                Header = BuildColumnHeader(columnName, columnTypeLabel, schemaColumn),
                SortMemberPath = columnName,
                IsReadOnly = !_inlineEditEligibility.IsEligible
                    || !_inlineEditEligibility.EditableColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase),
                CanUserSort = true,
                CellTemplate = new FuncDataTemplate<object?[]>((row, _) =>
                {
                    string rawValue = SqlEditorResultCellContentFormatter.FormatCellValue(row, capturedColumnIndex);
                    bool canExpandCell = SqlEditorResultCellContentFormatter.ShouldOfferExpandedView(rawValue);

                    var textBlock = new TextBlock
                    {
                        Text = rawValue,
                        Padding = new Avalonia.Thickness(12, 6),
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    };
                    if (IsDatabaseNull(row, capturedColumnIndex))
                    {
                        textBlock.FontStyle = FontStyle.Italic;
                        textBlock.Foreground = ResolveNullCellForegroundBrush();
                    }

                    textBlock.PointerPressed += (_, _) => CaptureGridCellContext(row, capturedColumnIndex);

                    var expandCellItem = new MenuItem { Header = L("sqlEditor.results.context.expandCell", "Expandir celula") };
                    expandCellItem.IsEnabled = canExpandCell;
                    expandCellItem.Click += async (_, _) =>
                    {
                        CaptureGridCellContext(row, capturedColumnIndex);
                        await ShowExpandedCellDialogAsync(row, capturedColumnIndex, columnName, columnTypeLabel);
                    };

                    var copyCellItem = new MenuItem { Header = L("sqlEditor.results.context.copyCell", "Copiar celula") };
                    copyCellItem.Click += async (_, _) =>
                    {
                        CaptureGridCellContext(row, capturedColumnIndex);
                        await CopyTextToClipboardAsync(SqlEditorResultCellContentFormatter.FormatCellValue(row, capturedColumnIndex));
                    };

                    var copyRowItem = new MenuItem { Header = L("sqlEditor.results.context.copyRow", "Copiar linha") };
                    copyRowItem.Click += async (_, _) =>
                    {
                        CaptureGridCellContext(row, capturedColumnIndex);
                        await CopyTextToClipboardAsync(BuildRowClipboardText(row));
                    };

                    var hideColumnItem = new MenuItem { Header = L("sqlEditor.results.context.hideColumn", "Ocultar coluna") };
                    hideColumnItem.Click += (_, _) =>
                    {
                        _viewModel?.HideResultColumn(columnName);
                    };

                    bool isPinned = _viewModel?.IsResultColumnPinned(columnName) == true;
                    var pinColumnItem = new MenuItem
                    {
                        Header = isPinned
                            ? L("sqlEditor.results.context.unpinColumn", "Desafixar coluna")
                            : L("sqlEditor.results.context.pinColumn", "Fixar coluna"),
                    };
                    pinColumnItem.Click += (_, _) =>
                    {
                        _viewModel?.SetResultColumnPinned(columnName, !isPinned);
                    };

                    textBlock.ContextMenu = new ContextMenu
                    {
                        ItemsSource = new object[] { expandCellItem, copyCellItem, copyRowItem, hideColumnItem, pinColumnItem },
                    };

                    textBlock.DoubleTapped += async (_, _) =>
                    {
                        if (!canExpandCell)
                            return;

                        await ShowExpandedCellDialogAsync(row, capturedColumnIndex, columnName, columnTypeLabel);
                    };

                    return textBlock;
                }),
                CellEditingTemplate = new FuncDataTemplate<object?[]>((row, _) =>
                {
                    string text = SqlEditorResultCellContentFormatter.FormatCellValue(row, capturedColumnIndex);
                    return new TextBox
                    {
                        Text = string.Equals(text, "NULL", StringComparison.Ordinal) ? string.Empty : text,
                        MinWidth = 80,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                }),
            });
        }

        int pinnedCount = _viewModel is null
            ? 0
            : displayColumns.Count(column => _viewModel.IsResultColumnPinned(column.ColumnName));
        ResultGrid.FrozenColumnCount = Math.Clamp(pinnedCount, 0, ResultGrid.Columns.Count);

        var rows = new List<object?[]>();
        foreach (object? rowItem in rowsView)
        {
            if (rowItem is not DataRowView rowView)
                continue;

            var values = new object?[table.Columns.Count];
            for (int i = 0; i < table.Columns.Count; i++)
                values[i] = rowView.Row[i];

            rows.Add(values);
        }

        rows = ApplyFilter(rows, table);
        rows = ApplySort(rows, table);

        int totalRows = table.Rows.Count;
        RowsCounterText.Text = rows.Count == totalRows
            ? string.Format(CultureInfo.InvariantCulture, L("sqlEditor.results.rows.countSingle", "{0} linhas"), totalRows)
            : string.Format(CultureInfo.InvariantCulture, L("sqlEditor.results.rows.countFiltered", "{0} de {1} linhas"), rows.Count, totalRows);

        var observableRows = new ObservableCollection<object?[]>(rows);

        ResultGrid.ItemsSource = observableRows;
        UpdateCounters();
    }

    private void ConfigureGridContextMenu()
    {
        var showAllColumnsItem = new MenuItem { Header = L("sqlEditor.results.context.showAllColumns", "Mostrar todas as colunas") };
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

    private void ResultsPaneButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        _viewModel.SelectedOutputPane = SqlEditorOutputPane.Results;
    }

    private void MessagesPaneButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        _viewModel.SelectedOutputPane = SqlEditorOutputPane.Messages;
    }

    private void ClearMessagesButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        _viewModel.ClearOutputMessages();
    }

    private async void ExportReportButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        if (!_viewModel.TryBuildReportExportContext(out SqlEditorReportExportContext? context) || context is null)
        {
            _viewModel.PublishStatus(
                L("sqlEditor.export.status.noResultTitle", "Nenhum resultado de execucao disponivel para exportacao."),
                L("sqlEditor.export.status.noResultDetail", "Execute uma consulta primeiro."),
                hasError: true);
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window owner || topLevel.StorageProvider is null)
            return;

        var dialogVm = new SqlEditorReportExportDialogViewModel(context.TabTitle);
        var dialog = new SqlEditorReportExportDialogWindow(dialogVm);

        await dialog.ShowDialog(owner);
        if (!dialog.WasConfirmed)
            return;

        string normalizedExtension = dialogVm.SuggestedExtension.TrimStart('.');
        var reportFileType = GetExportFileType(dialogVm.SelectedType?.Type ?? SqlEditorReportType.HtmlFullFeature);

        IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = L("sqlEditor.export.pickerTitle", "Exportar dados SQL"),
                DefaultExtension = normalizedExtension,
                SuggestedFileName = dialogVm.FileName,
                FileTypeChoices = [reportFileType],
            });

        string? outputPath = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(outputPath))
            return;

        try
        {
            SqlEditorReportExportRequest request = dialogVm.BuildRequest(outputPath);
            string writtenPath = await _reportExportService.ExportAsync(context, request);
            _viewModel.PublishStatus(L("sqlEditor.export.status.successTitle", "Relatorio exportado."), writtenPath);
        }
        catch (Exception ex)
        {
            _viewModel.PublishStatus(L("sqlEditor.export.status.failedTitle", "Falha ao exportar relatorio."), ex.Message, hasError: true);
        }
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
            : SqlEditorResultCellContentFormatter.FormatCellValue(_lastSelectedRow, _lastSelectedColumnIndex);

        await CopyTextToClipboardAsync(text);
        e.Handled = true;
    }

    private string BuildRowClipboardText(object?[]? row)
    {
        if (row is null || row.Length == 0)
            return string.Empty;

        var values = new string[row.Length];
        for (int i = 0; i < row.Length; i++)
            values[i] = SqlEditorResultCellContentFormatter.FormatCellValue(row, i);

        return string.Join('\t', values);
    }

    private async Task CopyTextToClipboardAsync(string text)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
            return;

        await topLevel.Clipboard.SetTextAsync(text ?? string.Empty);
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private void UpdateCounters()
    {
        if (_viewModel is null)
            return;

        if (_viewModel.IsMessagesOutputPaneSelected)
        {
            RowsCounterText.Text = string.Format(
                CultureInfo.InvariantCulture,
                L("sqlEditor.messages.count", "{0} mensagens"),
                _viewModel.OutputMessages.Count);
        }
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
            if (SqlEditorResultCellContentFormatter.FormatCellValue(row, i).Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string ToSortKey(object?[] row, int index)
    {
        string value = SqlEditorResultCellContentFormatter.FormatCellValue(row, index);
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double numeric))
            return numeric.ToString("00000000000000000000.000000", CultureInfo.InvariantCulture);

        return value;
    }

    private void OnResultGridCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (_viewModel is null || !_inlineEditEligibility.IsEligible)
            return;

        if (e.EditAction != DataGridEditAction.Commit)
            return;

        if (e.Row.DataContext is not object?[] row)
            return;

        string columnName = e.Column.SortMemberPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(columnName))
            return;

        if (!_inlineEditEligibility.EditableColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase))
            return;

        if (!_currentColumnIndexMap.TryGetValue(columnName, out int columnIndex))
            return;

        if (e.EditingElement is not TextBox textBox)
            return;

        string originalText = SqlEditorResultCellContentFormatter.FormatCellValue(row, columnIndex);
        string editedText = textBox.Text ?? string.Empty;
        if (string.Equals(originalText, editedText, StringComparison.Ordinal))
            return;

        _pendingInlineEdits[row] = new PendingInlineEdit(columnName, columnIndex, editedText);
    }

    private async void OnResultGridCellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
    {
        if (_viewModel is null || !_inlineEditEligibility.IsEligible)
            return;

        if (e.EditAction != DataGridEditAction.Commit)
            return;

        if (e.Row.DataContext is not object?[] row)
            return;

        if (!_pendingInlineEdits.TryGetValue(row, out PendingInlineEdit? pending))
            return;

        _pendingInlineEdits.Remove(row);
        try
        {
            string? tableFullName = _inlineEditEligibility.TableFullName;
            if (string.IsNullOrWhiteSpace(tableFullName))
                return;

            var pkValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (string pkColumn in _inlineEditEligibility.PrimaryKeyColumns)
            {
                if (!_currentColumnIndexMap.TryGetValue(pkColumn, out int pkIndex))
                    return;

                pkValues[pkColumn] = row[pkIndex];
            }

            object? editedValue = string.IsNullOrWhiteSpace(pending.EditedText)
                ? DBNull.Value
                : pending.EditedText;
            string updateSql = SqlInlineUpdateStatementBuilder.Build(
                _viewModel.ActiveTabProvider,
                tableFullName,
                pending.ColumnName,
                editedValue,
                pkValues);

            SqlEditorResultSet result = await _viewModel.ExecuteInlineUpdateAsync(updateSql);
            if (!result.Success)
                return;

            row[pending.ColumnIndex] = editedValue;
            ResultGrid.InvalidateVisual();
        }
        catch (Exception ex)
        {
            _viewModel.PublishStatus(
                L("sqlEditor.inlineEdit.failed", "Falha ao atualizar celula."),
                ex.Message,
                hasError: true);
        }
    }

    private void OnResultGridSorting(object? sender, DataGridColumnEventArgs e)
    {
        if (_viewModel is null)
            return;

        string column = e.Column.SortMemberPath ?? e.Column.Header?.ToString() ?? string.Empty;
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

    private void OnResultGridColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        if (_viewModel is null || ResultGrid.Columns.Count == 0)
            return;

        IReadOnlyList<string> order = ResultGrid.Columns
            .OrderBy(static column => column.DisplayIndex)
            .Select(column => column.SortMemberPath ?? column.Header?.ToString() ?? string.Empty)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToList();
        if (order.Count == 0)
            return;

        _viewModel.SetResultColumnOrder(order);
    }

    private IReadOnlyList<DataColumn> BuildDisplayColumns(DataTable table)
    {
        if (_viewModel is null || table.Columns.Count == 0)
            return table.Columns.Cast<DataColumn>().ToList();

        IReadOnlyList<string> order = _viewModel.ActiveTab.ResultColumnOrder;
        Dictionary<string, int> orderMap = order
            .Select((name, index) => (name, index))
            .GroupBy(static x => x.name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static g => g.Key, static g => g.First().index, StringComparer.OrdinalIgnoreCase);

        List<DataColumn> columns = table.Columns
            .Cast<DataColumn>()
            .Where(column => !_viewModel.IsResultColumnHidden(column.ColumnName))
            .OrderByDescending(column => _viewModel.IsResultColumnPinned(column.ColumnName))
            .ThenBy(column => orderMap.TryGetValue(column.ColumnName, out int index) ? index : int.MaxValue)
            .ThenBy(static column => column.Ordinal)
            .ToList();

        return columns;
    }

    private object BuildColumnHeader(
        string columnName,
        string columnTypeLabel,
        SqlEditorSchemaColumnItem? schemaColumn)
    {
        var titleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
        };

        titleRow.Children.Add(new MaterialIcon
        {
            Kind = schemaColumn?.TypeIcon ?? MaterialIconKind.TableColumn,
            Width = 12,
            Height = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ResolveResourceBrush("AccentPrimaryHoverBrush", Brushes.LightBlue),
        });
        titleRow.Children.Add(new TextBlock
        {
            Text = columnName,
            FontWeight = ResolveResourceFontWeight("FontWeightTitle", FontWeight.SemiBold),
        });
        if (schemaColumn?.IsPrimaryKey == true)
        {
            titleRow.Children.Add(new MaterialIcon
            {
                Kind = MaterialIconKind.KeyVariant,
                Width = 11,
                Height = 11,
                Foreground = new SolidColorBrush(Color.Parse("#5CE59D")),
            });
        }
        if (schemaColumn?.IsForeignKey == true)
        {
            var fkButton = new Button
            {
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Width = 14,
                Height = 14,
                Content = new MaterialIcon
                {
                    Kind = MaterialIconKind.SourceBranch,
                    Width = 11,
                    Height = 11,
                    Foreground = new SolidColorBrush(Color.Parse("#72B8FF")),
                },
            };
            ToolTip.SetTip(fkButton, L("sqlEditor.results.relationship.quick", "Abrir relacionamento"));
            fkButton.Click += async (_, _) => await ShowRelationshipDialogAsync(columnName);
            titleRow.Children.Add(fkButton);
        }
        if (schemaColumn?.IsIndexed == true)
        {
            titleRow.Children.Add(new MaterialIcon
            {
                Kind = MaterialIconKind.Magnify,
                Width = 11,
                Height = 11,
                Foreground = new SolidColorBrush(Color.Parse("#FFCF75")),
            });
        }

        var header = new StackPanel { Spacing = 0 };
        header.Children.Add(titleRow);
        header.Children.Add(new TextBlock
        {
            Text = columnTypeLabel,
            FontSize = ResolveResourceFontSize("FontSizeCaption", 11),
            Foreground = ResolveResourceBrush("TextMutedBrush", Brushes.Gray),
        });

        return header;
    }

    private async Task ShowRelationshipDialogAsync(string columnName)
    {
        if (_viewModel is null || string.IsNullOrWhiteSpace(columnName))
            return;

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window owner)
            return;

        IReadOnlyList<DBWeaver.Metadata.ForeignKeyRelation> relations = _viewModel.ResolveColumnRelationships(columnName);
        var dialog = new SqlEditorForeignKeyDialogWindow(columnName, relations);
        await dialog.ShowDialog(owner);
    }

    private async Task ShowExpandedCellDialogAsync(object?[]? row, int columnIndex, string columnName, string columnTypeLabel)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window owner)
            return;

        string rawValue = SqlEditorResultCellContentFormatter.FormatCellValue(row, columnIndex);
        if (!SqlEditorResultCellContentFormatter.ShouldOfferExpandedView(rawValue))
            return;

        string expandedValue = SqlEditorResultCellContentFormatter.FormatExpandedCellValue(rawValue);

        var dialog = new SqlEditorCellExpandDialogWindow(columnName, columnTypeLabel, expandedValue);
        await dialog.ShowDialog(owner);
    }

    private static bool IsDatabaseNull(object?[]? row, int index)
    {
        if (row is null || index < 0 || index >= row.Length)
            return false;

        return row[index] is DBNull;
    }

    private IBrush ResolveNullCellForegroundBrush()
    {
        if (_nullCellForegroundBrush is not null)
            return _nullCellForegroundBrush;

        if (Application.Current?.TryGetResource("TextSecondaryBrush", null, out object? resource) == true
            && resource is IBrush brush)
        {
            _nullCellForegroundBrush = brush;
            return brush;
        }

        _nullCellForegroundBrush = Brushes.Gray;
        return _nullCellForegroundBrush;
    }

    private static IBrush ResolveResourceBrush(string resourceKey, IBrush fallback)
    {
        if (Application.Current?.TryGetResource(resourceKey, null, out object? resource) == true
            && resource is IBrush brush)
        {
            return brush;
        }

        return fallback;
    }

    private static double ResolveResourceFontSize(string resourceKey, double fallback)
    {
        if (Application.Current?.TryGetResource(resourceKey, null, out object? resource) == true)
        {
            if (resource is double size)
                return size;
            if (resource is int intSize)
                return intSize;
        }

        return fallback;
    }

    private static FontWeight ResolveResourceFontWeight(string resourceKey, FontWeight fallback)
    {
        if (Application.Current?.TryGetResource(resourceKey, null, out object? resource) == true
            && resource is FontWeight fontWeight)
        {
            return fontWeight;
        }

        return fallback;
    }

    private static FilePickerFileType GetExportFileType(SqlEditorReportType reportType)
    {
        return reportType switch
        {
            SqlEditorReportType.JsonContract => new FilePickerFileType(L("sqlEditor.export.fileType.json", "Arquivo JSON"))
            {
                Patterns = ["*.json"],
                MimeTypes = ["application/json", "text/plain"],
            },
            SqlEditorReportType.CsvData => new FilePickerFileType(L("sqlEditor.export.fileType.csv", "Arquivo CSV"))
            {
                Patterns = ["*.csv"],
                MimeTypes = ["text/csv", "text/plain"],
            },
            SqlEditorReportType.ExcelWorkbook => new FilePickerFileType(L("sqlEditor.export.fileType.xlsx", "Pasta de trabalho Excel"))
            {
                Patterns = ["*.xlsx"],
                MimeTypes = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
            },
            _ => new FilePickerFileType(L("sqlEditor.export.fileType.html", "Arquivo HTML"))
            {
                Patterns = ["*.html", "*.htm"],
                MimeTypes = ["text/html", "text/plain"],
            },
        };
    }

    private sealed record PendingInlineEdit(
        string ColumnName,
        int ColumnIndex,
        string EditedText);

    private sealed class ObjectArrayReferenceComparer : IEqualityComparer<object?[]>
    {
        public static ObjectArrayReferenceComparer Instance { get; } = new();

        public bool Equals(object?[]? x, object?[]? y) => ReferenceEquals(x, y);

        public int GetHashCode(object?[] obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}


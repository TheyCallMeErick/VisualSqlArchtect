using Avalonia.Controls;
using AvaloniaEdit;
using System.ComponentModel;
using AkkornStudio.UI.Services;
using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.UI.Controls.Shell;

public partial class OutputPreviewModalControl : UserControl
{
    private OutputPreviewModalViewModel? _vm;

    public OutputPreviewModalControl()
    {
        InitializeComponent();
        ConfigureSqlEditors();
        DataContextChanged += (_, _) => AttachViewModel();
    }

    private void ConfigureSqlEditors()
    {
        TextEditor? querySqlEditor = this.FindControl<TextEditor>("QuerySqlEditor");
        if (querySqlEditor is not null)
        {
            querySqlEditor.IsReadOnly = true;
            querySqlEditor.Options.EnableHyperlinks = false;
            querySqlEditor.Options.EnableEmailHyperlinks = false;
            querySqlEditor.Options.HighlightCurrentLine = false;
            querySqlEditor.Options.AllowScrollBelowDocument = false;
            querySqlEditor.SyntaxHighlighting = SqlEditorHighlightingService.GetSqlDefinition();
        }

        TextEditor? ddlSqlEditor = this.FindControl<TextEditor>("DdlSqlEditor");
        if (ddlSqlEditor is not null)
        {
            ddlSqlEditor.IsReadOnly = true;
            ddlSqlEditor.Options.EnableHyperlinks = false;
            ddlSqlEditor.Options.EnableEmailHyperlinks = false;
            ddlSqlEditor.Options.HighlightCurrentLine = false;
            ddlSqlEditor.Options.AllowScrollBelowDocument = false;
            ddlSqlEditor.SyntaxHighlighting = SqlEditorHighlightingService.GetSqlDefinition();
        }
    }

    private void AttachViewModel()
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;

        _vm = DataContext as OutputPreviewModalViewModel;
        if (_vm is not null)
            _vm.PropertyChanged += OnViewModelPropertyChanged;

        SyncQuerySqlText();
        SyncDdlSqlText();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OutputPreviewModalViewModel.QuerySqlText))
            SyncQuerySqlText();

        if (e.PropertyName == nameof(OutputPreviewModalViewModel.DdlSqlText))
            SyncDdlSqlText();
    }

    private void SyncQuerySqlText()
    {
        if (_vm is null)
            return;

        TextEditor? querySqlEditor = this.FindControl<TextEditor>("QuerySqlEditor");
        if (querySqlEditor is null)
            return;

        string nextText = SqlDisplayFormatter.Format(_vm.QuerySqlText);
        if (string.Equals(querySqlEditor.Text, nextText, StringComparison.Ordinal))
            return;

        querySqlEditor.Text = nextText;
        querySqlEditor.TextArea.Caret.Offset = 0;
        querySqlEditor.TextArea.Caret.BringCaretToView();
    }

    private void SyncDdlSqlText()
    {
        if (_vm is null)
            return;

        TextEditor? ddlSqlEditor = this.FindControl<TextEditor>("DdlSqlEditor");
        if (ddlSqlEditor is null)
            return;

        string nextText = SqlDisplayFormatter.Format(_vm.DdlSqlText);
        if (string.Equals(ddlSqlEditor.Text, nextText, StringComparison.Ordinal))
            return;

        ddlSqlEditor.Text = nextText;
        ddlSqlEditor.TextArea.Caret.Offset = 0;
        ddlSqlEditor.TextArea.Caret.BringCaretToView();
    }

}

using System.Data;

namespace AkkornStudio.UI.ViewModels;

public sealed class SqlEditorResultTab
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required SqlEditorResultSet Result { get; init; }

    public DataView? RowsView => Result.Data?.DefaultView;
}

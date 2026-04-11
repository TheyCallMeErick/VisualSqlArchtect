namespace DBWeaver.UI.ViewModels;

public sealed class SqlEditorSchemaTableItem
{
    public required string Schema { get; init; }
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required IReadOnlyList<SqlEditorSchemaColumnItem> Columns { get; init; }
    public int PrimaryKeyCount { get; init; }
    public int ForeignKeyCount { get; init; }
    public int IndexedColumnCount { get; init; }
    public string SearchKey => $"{Schema}.{Name}".ToLowerInvariant();
}

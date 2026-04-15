using Material.Icons;

namespace DBWeaver.UI.ViewModels;

public sealed class SqlEditorSchemaColumnItem
{
    public required string Name { get; init; }
    public required string DataType { get; init; }
    public bool IsPrimaryKey { get; init; }
    public bool IsForeignKey { get; init; }
    public bool IsIndexed { get; init; }
    public bool IsUnique { get; init; }
    public string? RelatedTable { get; init; }
    public bool HasRelationship => !string.IsNullOrWhiteSpace(RelatedTable);
    public MaterialIconKind TypeIcon { get; init; } = MaterialIconKind.TableColumn;
}

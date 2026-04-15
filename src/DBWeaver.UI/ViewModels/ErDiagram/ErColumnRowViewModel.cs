using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.ViewModels.ErDiagram;

/// <summary>
/// Represents a single ER entity column row rendered in the ER canvas.
/// </summary>
public sealed class ErColumnRowViewModel : ViewModelBase
{
    public ErColumnRowViewModel(
        string columnName,
        string dataType,
        bool isNullable,
        bool isPrimaryKey,
        bool isForeignKey,
        bool isUnique,
        string? comment)
    {
        ColumnName = columnName;
        DataType = dataType;
        IsNullable = isNullable;
        IsPrimaryKey = isPrimaryKey;
        IsForeignKey = isForeignKey;
        IsUnique = isUnique;
        Comment = comment;
    }

    public string ColumnName { get; }

    public string DataType { get; }

    public bool IsNullable { get; }

    public bool IsPrimaryKey { get; }

    public bool IsForeignKey { get; }

    public bool IsUnique { get; }

    public string? Comment { get; }

    public string Badge => IsPrimaryKey ? "PK" : IsForeignKey ? "FK" : string.Empty;

    public bool HasBadge => !string.IsNullOrWhiteSpace(Badge);
}

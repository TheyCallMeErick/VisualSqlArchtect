using System.Collections.ObjectModel;
using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.UI.ViewModels.ErDiagram;

/// <summary>
/// Represents an entity node in the ER canvas.
/// </summary>
public sealed class ErEntityNodeViewModel : ViewModelBase
{
    private string _id;
    private string _schema;
    private string _name;
    private double _x;
    private double _y;
    private bool _isSelected;

    public ErEntityNodeViewModel(
        string schema,
        string name,
        bool isView,
        long? estimatedRowCount,
        IEnumerable<ErColumnRowViewModel>? columns = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Entity name cannot be empty.", nameof(name));

        _schema = schema?.Trim() ?? string.Empty;
        _name = name.Trim();
        _id = BuildCanonicalId(_schema, _name);

        IsView = isView;
        EstimatedRowCount = estimatedRowCount;
        Columns = columns is null
            ? []
            : new ObservableCollection<ErColumnRowViewModel>(columns);
    }

    public string Id
    {
        get => _id;
        private set => Set(ref _id, value);
    }

    public string Schema
    {
        get => _schema;
        private set => Set(ref _schema, value);
    }

    public string Name
    {
        get => _name;
        private set => Set(ref _name, value);
    }

    public string DisplayName => BuildCanonicalId(Schema, Name);

    public bool IsView { get; }

    public long? EstimatedRowCount { get; }

    public ObservableCollection<ErColumnRowViewModel> Columns { get; }

    public int ColumnCount => Columns.Count;

    public int PrimaryKeyCount => Columns.Count(column => column.IsPrimaryKey);

    public int ForeignKeyCount => Columns.Count(column => column.IsForeignKey);

    public string SelectionSummary =>
        $"{ColumnCount} coluna(s) · {PrimaryKeyCount} PK · {ForeignKeyCount} FK";

    public double X
    {
        get => _x;
        set => Set(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => Set(ref _y, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    public void HighlightColumns(IReadOnlySet<string> columnNames)
    {
        foreach (ErColumnRowViewModel column in Columns)
            column.IsRelationEndpointHighlighted = columnNames.Contains(column.ColumnName);
    }

    public void Rename(string newSchema, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Entity name cannot be empty.", nameof(newName));

        Schema = newSchema?.Trim() ?? string.Empty;
        Name = newName.Trim();
        Id = BuildCanonicalId(Schema, Name);
        RaisePropertyChanged(nameof(DisplayName));
    }

    private static string BuildCanonicalId(string schema, string name) =>
        string.IsNullOrWhiteSpace(schema) ? name : $"{schema}.{name}";
}

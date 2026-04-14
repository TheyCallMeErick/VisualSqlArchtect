using DBWeaver.Metadata;

namespace DBWeaver.UI.ViewModels.ErDiagram;

/// <summary>
/// Builds deterministic ER canvas state from database metadata.
/// </summary>
public static class ErCanvasBuilder
{
    private const double EntityWidth = 220;
    private const double HeaderHeight = 36;
    private const double ColumnRowHeight = 22;
    private const double HorizontalGap = 60;
    private const double VerticalGap = 40;
    private const int MaxEntitiesPerRow = 4;

    public static ErCanvasViewModel Build(DbMetadata metadata, bool includeViews = false)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var canvas = new ErCanvasViewModel
        {
            IncludeViews = includeViews,
        };

        IReadOnlyList<TableMetadata> eligibleTables = metadata.AllTables
            .Where(table => includeViews || table.Kind == TableKind.Table)
            .OrderBy(static table => table.Schema ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static table => table.Name, StringComparer.Ordinal)
            .ToList();

        var entityById = new Dictionary<string, ErEntityNodeViewModel>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < eligibleTables.Count; index++)
        {
            TableMetadata table = eligibleTables[index];
            ErEntityNodeViewModel entity = BuildEntity(table);

            int col = index % MaxEntitiesPerRow;
            int row = index / MaxEntitiesPerRow;
            double entityHeight = HeaderHeight + (entity.Columns.Count * ColumnRowHeight);

            entity.X = col * (EntityWidth + HorizontalGap);
            entity.Y = row * (entityHeight + VerticalGap);

            canvas.Entities.Add(entity);
            entityById[entity.Id] = entity;
        }

        IEnumerable<ForeignKeyRelation> orderedFks = metadata.AllForeignKeys
            .OrderBy(static fk => fk.ChildSchema ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static fk => fk.ChildTable, StringComparer.Ordinal)
            .ThenBy(static fk => fk.ChildColumn, StringComparer.Ordinal)
            .ThenBy(static fk => fk.ParentSchema ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static fk => fk.ParentTable, StringComparer.Ordinal)
            .ThenBy(static fk => fk.ParentColumn, StringComparer.Ordinal)
            .ThenBy(static fk => fk.ConstraintName, StringComparer.Ordinal);

        HashSet<string> ignoredCompositeKeys = [];
        var compositeGroups = metadata.AllForeignKeys
            .GroupBy(static fk => BuildCompositeGroupKey(fk), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToDictionary(
                static group => group.Key,
                static group => true,
                StringComparer.OrdinalIgnoreCase);

        foreach (ForeignKeyRelation fk in orderedFks)
        {
            string compositeKey = BuildCompositeGroupKey(fk);
            if (compositeGroups.ContainsKey(compositeKey))
            {
                if (ignoredCompositeKeys.Add(compositeKey))
                    canvas.AddTechnicalWarning("W-ER-READ-COMPOSITE-FK-IGNORED");

                continue;
            }

            string childEntityId = BuildEntityId(fk.ChildSchema, fk.ChildTable);
            string parentEntityId = BuildEntityId(fk.ParentSchema, fk.ParentTable);
            if (!entityById.ContainsKey(childEntityId) || !entityById.ContainsKey(parentEntityId))
                continue;

            canvas.Edges.Add(new ErRelationEdgeViewModel(
                constraintName: fk.ConstraintName,
                childEntityId: childEntityId,
                parentEntityId: parentEntityId,
                childColumn: fk.ChildColumn,
                parentColumn: fk.ParentColumn,
                onDelete: fk.OnDelete,
                onUpdate: fk.OnUpdate));
        }

        return canvas;
    }

    private static ErEntityNodeViewModel BuildEntity(TableMetadata table)
    {
        IEnumerable<ErColumnRowViewModel> columns = table.Columns.Select(column =>
            new ErColumnRowViewModel(
                columnName: column.Name,
                dataType: string.IsNullOrWhiteSpace(column.DataType) ? column.NativeType : column.DataType,
                isNullable: column.IsNullable,
                isPrimaryKey: column.IsPrimaryKey,
                isForeignKey: column.IsForeignKey,
                isUnique: column.IsUnique,
                comment: column.Comment));

        return new ErEntityNodeViewModel(
            schema: table.Schema,
            name: table.Name,
            isView: table.Kind != TableKind.Table,
            estimatedRowCount: table.EstimatedRowCount,
            columns: columns);
    }

    private static string BuildEntityId(string schema, string name) =>
        string.IsNullOrWhiteSpace(schema) ? name : $"{schema}.{name}";

    private static string BuildCompositeGroupKey(ForeignKeyRelation fk) =>
        $"{fk.ConstraintName}|{fk.ChildSchema}|{fk.ChildTable}|{fk.ParentSchema}|{fk.ParentTable}";
}

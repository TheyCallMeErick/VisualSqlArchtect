using AkkornStudio.Metadata;
using Avalonia;

namespace AkkornStudio.UI.ViewModels.ErDiagram;

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
    private const double ReverseLaneOffset = 48;
    private const double ForwardLaneOffset = 24;

    public static ErCanvasViewModel Build(DbMetadata metadata, bool includeViews = false)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var canvas = new ErCanvasViewModel();
        canvas.SetIncludeViewsSilently(includeViews);

        IReadOnlyList<TableMetadata> eligibleTables = metadata.AllTables
            .Where(table => includeViews || table.Kind == TableKind.Table)
            .OrderBy(static table => table.Schema ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static table => table.Name, StringComparer.Ordinal)
            .ToList();

        var entityById = new Dictionary<string, ErEntityNodeViewModel>(StringComparer.OrdinalIgnoreCase);
        var entities = new List<(ErEntityNodeViewModel Entity, double Height)>();
        foreach (TableMetadata table in eligibleTables)
        {
            ErEntityNodeViewModel entity = BuildEntity(table);
            double entityHeight = HeaderHeight + (entity.Columns.Count * ColumnRowHeight);
            entities.Add((entity, entityHeight));
        }

        int columnCount = Math.Max(1, MaxEntitiesPerRow);
        var columnBottom = new double[columnCount];

        for (int index = 0; index < entities.Count; index++)
        {
            int col = GetShortestColumnIndex(columnBottom);
            ErEntityNodeViewModel entity = entities[index].Entity;
            double entityHeight = entities[index].Height;

            entity.X = col * (EntityWidth + HorizontalGap);
            entity.Y = columnBottom[col];
            columnBottom[col] += entityHeight + VerticalGap;

            canvas.Entities.Add(entity);
            entityById[entity.Id] = entity;
        }

        IReadOnlyList<IGrouping<string, ForeignKeyRelation>> fkGroups = metadata.AllForeignKeys
            .OrderBy(static fk => fk.ChildSchema ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static fk => fk.ChildTable, StringComparer.Ordinal)
            .ThenBy(static fk => fk.ParentSchema ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static fk => fk.ParentTable, StringComparer.Ordinal)
            .ThenBy(static fk => fk.ConstraintName, StringComparer.Ordinal)
            .GroupBy(static fk => BuildCompositeGroupKey(fk), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (IGrouping<string, ForeignKeyRelation> group in fkGroups)
        {
            ForeignKeyRelation fk = group
                .OrderBy(static item => item.OrdinalPosition)
                .ThenBy(static item => item.ChildColumn, StringComparer.Ordinal)
                .First();

            string childEntityId = BuildEntityId(fk.ChildSchema, fk.ChildTable);
            string parentEntityId = BuildEntityId(fk.ParentSchema, fk.ParentTable);
            if (!entityById.ContainsKey(childEntityId) || !entityById.ContainsKey(parentEntityId))
                continue;

            IReadOnlyList<string> childColumns = group
                .OrderBy(static item => item.OrdinalPosition)
                .Select(static item => item.ChildColumn)
                .ToList();
            IReadOnlyList<string> parentColumns = group
                .OrderBy(static item => item.OrdinalPosition)
                .Select(static item => item.ParentColumn)
                .ToList();

            canvas.Edges.Add(new ErRelationEdgeViewModel(
                constraintName: fk.ConstraintName,
                childEntityId: childEntityId,
                parentEntityId: parentEntityId,
                childColumns: childColumns,
                parentColumns: parentColumns,
                onDelete: fk.OnDelete,
                onUpdate: fk.OnUpdate));
        }

        UpdateEdgeGeometry(canvas, entityById);

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

    private static void UpdateEdgeGeometry(
        ErCanvasViewModel canvas,
        IReadOnlyDictionary<string, ErEntityNodeViewModel> entityById)
    {
        for (int index = 0; index < canvas.Edges.Count; index++)
        {
            ErRelationEdgeViewModel edge = canvas.Edges[index];
            if (!entityById.TryGetValue(edge.ChildEntityId, out ErEntityNodeViewModel? child))
                continue;

            if (!entityById.TryGetValue(edge.ParentEntityId, out ErEntityNodeViewModel? parent))
                continue;

            double childHeight = HeaderHeight + (child.Columns.Count * ColumnRowHeight);
            double parentHeight = HeaderHeight + (parent.Columns.Count * ColumnRowHeight);
            bool childIsLeftOfParent = child.X <= parent.X;
            edge.StartX = childIsLeftOfParent ? child.X + EntityWidth : child.X;
            edge.StartY = child.Y + (childHeight / 2d);
            edge.EndX = childIsLeftOfParent ? parent.X : parent.X + EntityWidth;
            edge.EndY = parent.Y + (parentHeight / 2d);
            edge.SetRoute(BuildOrthogonalRoute(edge, child, parent, index));
        }
    }

    private static IReadOnlyList<Point> BuildOrthogonalRoute(
        ErRelationEdgeViewModel edge,
        ErEntityNodeViewModel child,
        ErEntityNodeViewModel parent,
        int edgeIndex)
    {
        double startX = edge.StartX;
        double startY = edge.StartY;
        double endX = edge.EndX;
        double endY = edge.EndY;
        double laneOffset = ((edgeIndex % 3) + 1);
        bool flowsLeftToRight = startX <= endX;

        if (flowsLeftToRight)
        {
            double midX = startX + ((endX - startX) / 2d) + (ForwardLaneOffset * laneOffset);
            return
            [
                new Point(startX, startY),
                new Point(midX, startY),
                new Point(midX, endY),
                new Point(endX, endY),
            ];
        }

        double corridorX = Math.Max(child.X + EntityWidth, parent.X + EntityWidth) + (ReverseLaneOffset * laneOffset);
        return
        [
            new Point(startX, startY),
            new Point(corridorX, startY),
            new Point(corridorX, endY),
            new Point(endX, endY),
        ];
    }

    private static int GetShortestColumnIndex(IReadOnlyList<double> columnBottom)
    {
        int bestIndex = 0;
        double bestValue = columnBottom[0];
        for (int index = 1; index < columnBottom.Count; index++)
        {
            if (columnBottom[index] >= bestValue)
                continue;

            bestValue = columnBottom[index];
            bestIndex = index;
        }

        return bestIndex;
    }
}

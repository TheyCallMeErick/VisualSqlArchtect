using System.Collections.ObjectModel;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels.UndoRedo.Commands;

namespace VisualSqlArchitect.UI.ViewModels.Canvas;

/// <summary>
/// Manages pin connections on the canvas.
/// Handles wire creation and deletion.
/// </summary>
public sealed class PinManager(
    ObservableCollection<NodeViewModel> nodes,
    ObservableCollection<ConnectionViewModel> connections,
    UndoRedoStack undoRedo
) : IPinManager
{
    private readonly ObservableCollection<NodeViewModel> _nodes = nodes;
    private readonly ObservableCollection<ConnectionViewModel> _connections = connections;
    private readonly UndoRedoStack _undoRedo = undoRedo;

    /// <summary>
    /// Creates a new connection between two pins.
    /// </summary>
    public void ConnectPins(PinViewModel from, PinViewModel to)
    {
        PinViewModel src = from.Direction == PinDirection.Output ? from : to;
        PinViewModel dest = from.Direction == PinDirection.Input ? from : to;

        if (!dest.CanAccept(src))
            return;

        bool shouldPruneSameTableProjectionWires = IsWildcardProjectionToColumnList(src, dest);
        UndoRedoStack.UndoRedoTransaction? transaction = null;

        if (shouldPruneSameTableProjectionWires && !_undoRedo.InTransaction)
            transaction = _undoRedo.BeginTransaction("Connect wildcard projection");

        try
        {
            ConnectionViewModel created = AddConnection(src, dest);
            if (shouldPruneSameTableProjectionWires)
            {
                foreach (ConnectionViewModel redundant in FindRedundantProjectionConnections(created))
                    _undoRedo.Execute(new DeleteConnectionCommand(redundant));
            }

            ApplyComparisonConcretization(src, dest);
            transaction?.Commit();
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    private ConnectionViewModel AddConnection(PinViewModel src, PinViewModel dest)
    {
        ConnectionViewModel? displaced = dest.AllowMultiple
            ? null
            : _connections.FirstOrDefault(c => c.ToPin == dest);
        var conn = new ConnectionViewModel(src, src.AbsolutePosition, dest.AbsolutePosition)
        {
            ToPin = dest,
        };
        _undoRedo.Execute(new AddConnectionCommand(conn, displaced));
        return conn;
    }

    /// <summary>
    /// Deletes an existing connection from the canvas.
    /// </summary>
    public void DeleteConnection(ConnectionViewModel conn) =>
        _undoRedo.Execute(new DeleteConnectionCommand(conn));

    /// <summary>
    /// Legacy method kept for compatibility while narrowing is phased out.
    /// </summary>
    private static void NarrowPinTypes(PinViewModel srcPin, PinViewModel destPin)
    {
        _ = srcPin;
        _ = destPin;
    }

    /// <summary>
    /// Legacy method kept for compatibility while narrowing is phased out.
    /// </summary>
    public void ClearNarrowingIfNeeded(IEnumerable<NodeViewModel> nodes)
    {
        RecomputeComparisonConcretization(nodes);
    }

    private void ApplyComparisonConcretization(PinViewModel src, PinViewModel dest)
    {
        if (dest.Direction != PinDirection.Input)
            return;

        if (!IsComparisonNode(dest.Owner.Type) || dest.EffectiveDataType != PinDataType.ColumnRef)
            return;

        PinDataType? scalarType = ResolveSourceScalarType(src);
        if (scalarType is null)
            return;

        foreach (PinViewModel input in dest.Owner.InputPins)
        {
            if (input.DataType == PinDataType.ColumnRef && input.ColumnRefMeta is null)
                input.ExpectedColumnScalarType = scalarType;
        }
    }

    private void RecomputeComparisonConcretization(IEnumerable<NodeViewModel> nodes)
    {
        foreach (NodeViewModel node in nodes)
        {
            if (!IsComparisonNode(node.Type))
                continue;

            foreach (PinViewModel input in node.InputPins)
            {
                if (input.DataType == PinDataType.ColumnRef && input.ColumnRefMeta is null)
                    input.ExpectedColumnScalarType = null;
            }

            PinDataType? concrete = _connections
                .Where(c => c.ToPin?.Owner == node && c.ToPin.Direction == PinDirection.Input)
                .Select(c => ResolveSourceScalarType(c.FromPin))
                .FirstOrDefault(t => t is not null);

            if (concrete is null)
                continue;

            foreach (PinViewModel input in node.InputPins)
            {
                if (input.DataType == PinDataType.ColumnRef && input.ColumnRefMeta is null)
                    input.ExpectedColumnScalarType = concrete;
            }
        }
    }

    private static PinDataType? ResolveSourceScalarType(PinViewModel source)
    {
        if (source.EffectiveDataType == PinDataType.ColumnRef)
            return source.ColumnRefMeta?.ScalarType ?? source.ExpectedColumnScalarType;

        if (source.EffectiveDataType.IsScalar())
            return source.EffectiveDataType;

        return null;
    }

    private static bool IsComparisonNode(NodeType type) =>
        type is NodeType.Equals
            or NodeType.NotEquals
            or NodeType.GreaterThan
            or NodeType.GreaterOrEqual
            or NodeType.LessThan
            or NodeType.LessOrEqual
            or NodeType.Between
            or NodeType.NotBetween
            or NodeType.IsNull
            or NodeType.IsNotNull;

    private static bool IsWildcardProjectionToColumnList(PinViewModel sourcePin, PinViewModel destinationPin)
    {
        if (sourcePin.Owner.Type != NodeType.TableSource
            || sourcePin.EffectiveDataType != PinDataType.ColumnSet
            || !sourcePin.Name.Equals("*", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (destinationPin.Owner.Type != NodeType.ColumnList)
            return false;

        return destinationPin.Name.Equals("columns", StringComparison.OrdinalIgnoreCase)
            || destinationPin.Name.Equals("metadata", StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<ConnectionViewModel> FindRedundantProjectionConnections(ConnectionViewModel wildcardConnection)
    {
        if (wildcardConnection.ToPin is null)
            return [];

        PinViewModel destinationPin = wildcardConnection.ToPin;
        if (!IsWildcardProjectionToColumnList(wildcardConnection.FromPin, destinationPin))
            return [];

        NodeViewModel projectionNode = destinationPin.Owner;
        NodeViewModel sourceTable = wildcardConnection.FromPin.Owner;

        return _connections
            .Where(c =>
                c != wildcardConnection
                && c.ToPin is not null
                && c.ToPin.Owner == projectionNode
                && (c.ToPin.Name.Equals("columns", StringComparison.OrdinalIgnoreCase)
                    || c.ToPin.Name.Equals("metadata", StringComparison.OrdinalIgnoreCase))
                && c.FromPin.Owner == sourceTable)
            .ToList();
    }

}

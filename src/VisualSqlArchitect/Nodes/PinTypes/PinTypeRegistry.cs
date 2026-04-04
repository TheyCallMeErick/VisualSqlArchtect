namespace VisualSqlArchitect.Nodes.PinTypes;

/// <summary>
/// Registry and adapter for <see cref="IPinDataType"/> implementations.
/// Provides mapping between legacy <see cref="PinDataType"/> enum values and <see cref="IPinDataType"/> instances.
/// Used during serialization round-trips and for backward compatibility.
/// </summary>
public static class PinTypeRegistry
{
    private sealed class StandardPinDataType(
        PinDataType enumType,
        string visualColorHex,
        double wireThickness,
        PinWireDashKind wireDashKind,
        bool isDdlFamily
    ) : IPinDataType
    {
        public PinDataType EnumType { get; } = enumType;
        public string Name => EnumType.ToString();
        public string VisualColorHex { get; } = visualColorHex;
        public double WireThickness { get; } = wireThickness;
        public PinWireDashKind WireDashKind { get; } = wireDashKind;
        public bool IsDdlFamily { get; } = isDdlFamily;

        public bool CanReceiveFrom(IPinDataType source)
        {
            if (source is not StandardPinDataType src)
                return false;

            return IsDdlFamily == src.IsDdlFamily;
        }
    }

    private static readonly IReadOnlyDictionary<PinDataType, StandardPinDataType> _types =
        new Dictionary<PinDataType, StandardPinDataType>
        {
            [PinDataType.Text] = new(PinDataType.Text, "#60A5FA", 1.8, PinWireDashKind.Solid, false),
            [PinDataType.Integer] = new(PinDataType.Integer, "#34D399", 1.8, PinWireDashKind.Solid, false),
            [PinDataType.Decimal] = new(PinDataType.Decimal, "#86EFAC", 1.8, PinWireDashKind.Solid, false),
            [PinDataType.Number] = new(PinDataType.Number, "#4ADE80", 1.8, PinWireDashKind.Solid, false),
            [PinDataType.Boolean] = new(PinDataType.Boolean, "#FCD34D", 1.8, PinWireDashKind.Solid, false),
            [PinDataType.DateTime] = new(PinDataType.DateTime, "#38BDF8", 1.8, PinWireDashKind.Solid, false),
            [PinDataType.Json] = new(PinDataType.Json, "#818CF8", 1.8, PinWireDashKind.Solid, false),
            [PinDataType.ColumnRef] = new(PinDataType.ColumnRef, "#FB923C", 2.0, PinWireDashKind.Solid, false),
            [PinDataType.ColumnSet] = new(PinDataType.ColumnSet, "#FBBF24", 2.2, PinWireDashKind.LongDash, false),
            [PinDataType.RowSet] = new(PinDataType.RowSet, "#F472B6", 2.5, PinWireDashKind.WideDash, false),
            [PinDataType.TableDef] = new(PinDataType.TableDef, "#2563EB", 2.5, PinWireDashKind.Solid, true),
            [PinDataType.ViewDef] = new(PinDataType.ViewDef, "#0EA5E9", 2.4, PinWireDashKind.Solid, true),
            [PinDataType.ColumnDef] = new(PinDataType.ColumnDef, "#16A34A", 2.0, PinWireDashKind.Solid, true),
            [PinDataType.Constraint] = new(PinDataType.Constraint, "#A78BFA", 2.2, PinWireDashKind.MediumDash, true),
            [PinDataType.IndexDef] = new(PinDataType.IndexDef, "#93C5FD", 1.8, PinWireDashKind.ShortDash, true),
            [PinDataType.TypeDef] = new(PinDataType.TypeDef, "#22D3EE", 2.2, PinWireDashKind.MediumDash, true),
            [PinDataType.SequenceDef] = new(PinDataType.SequenceDef, "#22D3EE", 2.2, PinWireDashKind.Solid, true),
            [PinDataType.AlterOp] = new(PinDataType.AlterOp, "#F59E0B", 2.2, PinWireDashKind.LongDash, true),
            [PinDataType.Expression] = new(PinDataType.Expression, "#6B7280", 1.5, PinWireDashKind.Dotted, false),
        };

    public static IPinDataType GetType(PinDataType type)
        => _types.TryGetValue(type, out StandardPinDataType? value)
            ? value
            : _types[PinDataType.Expression];

    public static PinDataType GetEnum(IPinDataType type)
    {
        if (type is StandardPinDataType mapped)
            return mapped.EnumType;

        foreach ((PinDataType key, StandardPinDataType value) in _types)
            if (string.Equals(value.Name, type.Name, StringComparison.Ordinal))
                return key;

        return PinDataType.Expression;
    }

    public static bool IsDdlFamily(PinDataType type)
        => GetType(type).CanReceiveFrom(GetType(PinDataType.TableDef));
}

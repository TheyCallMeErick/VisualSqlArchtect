using System.Reflection;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Controls;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Controls;

public class PinShapeControlDdlGeometryMappingTests
{
    private static readonly MethodInfo ResolveGeometryKindMethod = typeof(PinShapeControl).GetMethod(
        "ResolveGeometryKind",
        BindingFlags.NonPublic | BindingFlags.Static
    )!;

    [Theory]
    [InlineData(PinDataType.TableDef, "TableDefRoundedSquare")]
    [InlineData(PinDataType.ColumnDef, "ColumnDefDoubleCircle")]
    [InlineData(PinDataType.Constraint, "ConstraintDiamond")]
    [InlineData(PinDataType.IndexDef, "IndexDefTriangle")]
    [InlineData(PinDataType.AlterOp, "AlterOpRoundedArrow")]
    public void ResolveGeometryKind_MapsDdlTypesToDedicatedShapes(PinDataType type, string expectedGeometry)
    {
        object? value = ResolveGeometryKindMethod.Invoke(null, [type]);

        Assert.NotNull(value);
        Assert.Equal(expectedGeometry, value!.ToString());
    }
}

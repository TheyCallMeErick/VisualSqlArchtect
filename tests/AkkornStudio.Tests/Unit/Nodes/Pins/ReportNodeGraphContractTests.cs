using AkkornStudio.Nodes;

namespace AkkornStudio.Tests.Unit.Nodes.Pins;

public sealed class ReportNodeGraphContractTests
{
    [Fact]
    public void LegacyReportNodes_AreRemovedFromRuntimeEnum()
    {
        Assert.False(Enum.TryParse<NodeType>("RawSqlQuery", out _));
        Assert.False(Enum.TryParse<NodeType>("ReportOutput", out _));
    }

    [Fact]
    public void LegacyReportPinType_IsRemovedFromRuntimeEnum()
    {
        Assert.False(Enum.TryParse<PinDataType>("ReportQuery", out _));
    }
}

using AkkornStudio.UI.ViewModels.Canvas;

namespace AkkornStudio.UI.Services.Explain;

public interface ISqlServerExplainPlanParser
{
    SqlServerParsedPlan Parse(string rawXml);
}




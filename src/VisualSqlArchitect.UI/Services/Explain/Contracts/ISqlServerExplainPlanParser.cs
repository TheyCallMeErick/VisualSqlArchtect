using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services.Explain;

public interface ISqlServerExplainPlanParser
{
    SqlServerParsedPlan Parse(string rawXml);
}




using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services.Explain;

public interface IMySqlExplainPlanParser
{
    MySqlParsedPlan ParseJson(string rawJson);
    MySqlParsedPlan ParseAnalyze(string rawAnalyzeText);
}




using AkkornStudio.UI.ViewModels.Canvas;

namespace AkkornStudio.UI.Services.Explain;

public interface IMySqlExplainPlanParser
{
    MySqlParsedPlan ParseJson(string rawJson);
    MySqlParsedPlan ParseAnalyze(string rawAnalyzeText);
}




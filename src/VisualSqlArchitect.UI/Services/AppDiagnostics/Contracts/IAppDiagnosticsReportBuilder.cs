using VisualSqlArchitect.UI.Services.AppDiagnostics.Models;

namespace VisualSqlArchitect.UI.Services.AppDiagnostics.Contracts;

public interface IAppDiagnosticsReportBuilder
{
    string BuildReport(string overallLabel, IEnumerable<AppDiagnosticCategoryViewModel> categories);
}

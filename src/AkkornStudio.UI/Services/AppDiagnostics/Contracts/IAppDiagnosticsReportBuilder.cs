using AkkornStudio.UI.Services.AppDiagnostics.Models;

namespace AkkornStudio.UI.Services.AppDiagnostics.Contracts;

public interface IAppDiagnosticsReportBuilder
{
    string BuildReport(string overallLabel, IEnumerable<AppDiagnosticCategoryViewModel> categories);
}

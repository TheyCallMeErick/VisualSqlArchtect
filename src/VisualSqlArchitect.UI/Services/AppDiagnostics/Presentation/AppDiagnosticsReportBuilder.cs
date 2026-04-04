using System.Text;
using VisualSqlArchitect.UI.Services.AppDiagnostics.Contracts;
using VisualSqlArchitect.UI.Services.AppDiagnostics.Models;
using VisualSqlArchitect.UI.Services.Localization;

namespace VisualSqlArchitect.UI.Services.AppDiagnostics.Presentation;

public sealed class AppDiagnosticsReportBuilder(ILocalizationService localization) : IAppDiagnosticsReportBuilder
{
    private readonly ILocalizationService _localization = localization;

    public string BuildReport(string overallLabel, IEnumerable<AppDiagnosticCategoryViewModel> categories)
    {
        var sb = new StringBuilder();
        sb.AppendLine(L("diagnostics.report.title", "Visual SQL Architect - Diagnostic Report"));
        sb.AppendLine($"{L("diagnostics.report.generated", "Generated")}: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"{L("diagnostics.report.overall", "Overall")}: {overallLabel}");
        sb.AppendLine(new string('─', 50));

        foreach (AppDiagnosticCategoryViewModel category in categories)
        {
            IReadOnlyList<AppDiagnosticEntry> snapshot = category.SnapshotItems();
            if (snapshot.Count == 0)
                continue;

            sb.AppendLine($"[{category.Title}]");
            foreach (AppDiagnosticEntry entry in snapshot)
            {
                sb.AppendLine($"- {entry.Name} [{entry.Status}]");
                sb.AppendLine($"  {L("diagnostics.report.details", "Details")}: {entry.Details}");
                sb.AppendLine($"  {L("diagnostics.report.recommendation", "Recommendation")}: {entry.Recommendation}");
                sb.AppendLine($"  {L("diagnostics.report.lastCheck", "Last Check")}: {entry.LastCheckLabel}");
            }
        }

        return sb.ToString();
    }

    private string L(string key, string fallback)
    {
        string value = _localization[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}

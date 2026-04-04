using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using VisualSqlArchitect.UI.Services.SqlImport.Build;
using VisualSqlArchitect.UI.Services.SqlImport.Execution.Parsing;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services.SqlImport.Execution.Applying;

internal sealed class SqlImportGroupingClauseApplier : ISqlImportApplyStep
{
    public SqlImportApplyResult Apply(SqlImportApplyContext context)
    {
        SqlImportParsedQuery query = context.Query;
        ImportBuildContext coreContext = context.CoreContext;
        ObservableCollection<ImportReportItem> report = context.Report;
        CancellationToken cancellationToken = context.CancellationToken;

        if (query.GroupBy is null)
            return new SqlImportApplyResult(0, 0, 0);

        int imported = 0;
        int partial = 0;
        int skipped = 0;

        NodeViewModel result = coreContext.ResultNode;
        var tableNodes = coreContext.TableNodes;
        var projectedAliases = coreContext.ProjectedAliases;

        string[] terms = query.GroupBy.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var importedGroupTerms = new List<string>();
        int importedTerms = 0;

        foreach (string term in terms)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Match termMatch = Regex.Match(term, @"^(\w+(?:\.\w+)?)$", RegexOptions.IgnoreCase);
            if (!termMatch.Success)
                continue;

            string expr = termMatch.Groups[1].Value.Trim();
            string colName = expr.Split('.').Last();

            PinViewModel? groupPin;
            if (projectedAliases.TryGetValue(colName, out PinViewModel? aliasedPin))
                groupPin = aliasedPin;
            else
            {
                groupPin = tableNodes
                    .SelectMany(n => n.OutputPins)
                    .FirstOrDefault(p => p.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));
            }

            if (groupPin is null)
                continue;

            importedGroupTerms.Add(string.Join('|', groupPin.Owner.Id, groupPin.Name));
            importedTerms++;
        }

        if (importedGroupTerms.Count > 0)
            result.Parameters["import_group_terms"] = string.Join(';', importedGroupTerms);

        if (importedTerms == terms.Length)
        {
            report.Add(new ImportReportItem($"GROUP BY {SqlImportClauseApplyUtilities.Truncate(query.GroupBy, 30)}", EImportItemStatus.Imported, sourceNodeId: result.Id));
            imported++;
        }
        else if (importedTerms > 0)
        {
            report.Add(
                new ImportReportItem(
                    $"GROUP BY {SqlImportClauseApplyUtilities.Truncate(query.GroupBy, 30)}",
                    EImportItemStatus.Partial,
                    "Some grouping terms could not be mapped and were skipped",
                    result.Id
                )
            );
            partial++;
        }
        else
        {
            report.Add(
                new ImportReportItem(
                    $"GROUP BY {SqlImportClauseApplyUtilities.Truncate(query.GroupBy, 30)}",
                    EImportItemStatus.Skipped,
                    "Unsupported grouping expression - add manually"
                )
            );
            skipped++;
        }

        var groupedTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string term in terms)
        {
            Match groupedTermMatch = Regex.Match(term, @"^(\w+(?:\.\w+)?)$", RegexOptions.IgnoreCase);
            if (!groupedTermMatch.Success)
                continue;

            string groupedExpr = groupedTermMatch.Groups[1].Value.Trim();
            groupedTerms.Add(groupedExpr);
            groupedTerms.Add(groupedExpr.Split('.').Last());
        }

        foreach (SqlImportSelectedColumn selectedColumn in query.SelectedColumns)
        {
            if (SqlImportClauseApplyUtilities.LooksLikeAggregateExpression(selectedColumn.Expr))
                continue;

            string exprTrimmed = selectedColumn.Expr.Trim();
            string exprShort = exprTrimmed.Split('.').Last();
            bool isGrouped = groupedTerms.Contains(exprTrimmed)
                || groupedTerms.Contains(exprShort)
                || (!string.IsNullOrWhiteSpace(selectedColumn.Alias) && groupedTerms.Contains(selectedColumn.Alias.Trim()));

            if (isGrouped)
                continue;

            report.Add(
                new ImportReportItem(
                    $"GROUP BY conflict: {SqlImportClauseApplyUtilities.Truncate(exprTrimmed, 40)}",
                    EImportItemStatus.Partial,
                    "Selected column is neither grouped nor aggregated"
                )
            );
            partial++;
        }

        return new SqlImportApplyResult(imported, partial, skipped);
    }
}

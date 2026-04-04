using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using VisualSqlArchitect.UI.Services.SqlImport.Build;
using VisualSqlArchitect.UI.Services.SqlImport.Execution.Parsing;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services.SqlImport.Execution.Applying;

internal sealed class SqlImportOrderingClauseApplier : ISqlImportApplyStep
{
    public SqlImportApplyResult Apply(SqlImportApplyContext context)
    {
        SqlImportParsedQuery query = context.Query;
        ImportBuildContext coreContext = context.CoreContext;
        ObservableCollection<ImportReportItem> report = context.Report;
        CancellationToken cancellationToken = context.CancellationToken;

        if (query.OrderBy is null)
            return new SqlImportApplyResult(0, 0, 0);

        int imported = 0;
        int partial = 0;
        int skipped = 0;

        NodeViewModel result = coreContext.ResultNode;
        var tableNodes = coreContext.TableNodes;
        var projectedAliases = coreContext.ProjectedAliases;

        string[] terms = query.OrderBy.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var importedOrderTerms = new List<string>();
        int importedTerms = 0;

        foreach (string term in terms)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Match termMatch = Regex.Match(
                term,
                @"^(\w+(?:\.\w+)?)(?:\s+(ASC|DESC))?$",
                RegexOptions.IgnoreCase
            );

            if (!termMatch.Success)
                continue;

            string expr = termMatch.Groups[1].Value.Trim();
            string colName = expr.Split('.').Last();
            bool desc = termMatch.Groups[2].Success && termMatch.Groups[2].Value.Equals("DESC", StringComparison.OrdinalIgnoreCase);

            PinViewModel? orderPin;
            if (projectedAliases.TryGetValue(colName, out PinViewModel? aliasedPin))
                orderPin = aliasedPin;
            else
            {
                orderPin = tableNodes
                    .SelectMany(n => n.OutputPins)
                    .FirstOrDefault(p => p.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));
            }

            if (orderPin is null)
                continue;

            importedOrderTerms.Add(string.Join('|', orderPin.Owner.Id, orderPin.Name, desc ? "DESC" : "ASC"));
            importedTerms++;
        }

        if (importedOrderTerms.Count > 0)
            result.Parameters["import_order_terms"] = string.Join(';', importedOrderTerms);

        if (importedTerms == terms.Length)
        {
            report.Add(new ImportReportItem($"ORDER BY {SqlImportClauseApplyUtilities.Truncate(query.OrderBy, 30)}", EImportItemStatus.Imported, sourceNodeId: result.Id));
            imported++;
        }
        else if (importedTerms > 0)
        {
            report.Add(
                new ImportReportItem(
                    $"ORDER BY {SqlImportClauseApplyUtilities.Truncate(query.OrderBy, 30)}",
                    EImportItemStatus.Partial,
                    "Some sort terms could not be mapped and were skipped",
                    result.Id
                )
            );
            partial++;
        }
        else
        {
            report.Add(
                new ImportReportItem(
                    $"ORDER BY {SqlImportClauseApplyUtilities.Truncate(query.OrderBy, 30)}",
                    EImportItemStatus.Skipped,
                    "Unsupported sort expression - add manually"
                )
            );
            skipped++;
        }

        return new SqlImportApplyResult(imported, partial, skipped);
    }
}

using System.Collections.ObjectModel;
using AkkornStudio.UI.Services.SqlImport.Build;
using AkkornStudio.UI.Services.SqlImport.Execution.Parsing;
using AkkornStudio.UI.ViewModels;
using AkkornStudio.UI.ViewModels.Canvas;

namespace AkkornStudio.UI.Services.SqlImport.Execution.Applying;

public sealed class SqlImportClauseApplier(CanvasViewModel canvas)
{
    private readonly IReadOnlyList<ISqlImportApplyStep> _steps =
    [
        new SqlImportWhereClauseApplier(canvas),
        new SqlImportOrderingClauseApplier(),
        new SqlImportGroupingClauseApplier(),
        new SqlImportHavingClauseApplier(canvas),
        new SqlImportResultModifiersApplier(canvas),
    ];

    public SqlImportApplyResult Apply(
        SqlImportParsedQuery query,
        ImportBuildContext coreContext,
        ObservableCollection<ImportReportItem> report,
        CancellationToken cancellationToken
    )
    {
        int imported = 0;
        int partial = 0;
        int skipped = 0;

        var context = new SqlImportApplyContext(query, coreContext, report, cancellationToken);

        foreach (ISqlImportApplyStep step in _steps)
        {
            SqlImportApplyResult stepResult = step.Apply(context);
            imported += stepResult.Imported;
            partial += stepResult.Partial;
            skipped += stepResult.Skipped;
        }

        return new SqlImportApplyResult(imported, partial, skipped);
    }
}

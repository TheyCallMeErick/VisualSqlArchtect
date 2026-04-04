using System.Collections.ObjectModel;
using VisualSqlArchitect.UI.Services.SqlImport.Build;
using VisualSqlArchitect.UI.Services.SqlImport.Execution.Parsing;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services.SqlImport.Execution.Applying;

internal sealed record SqlImportApplyContext(
    SqlImportParsedQuery Query,
    ImportBuildContext CoreContext,
    ObservableCollection<ImportReportItem> Report,
    CancellationToken CancellationToken
);

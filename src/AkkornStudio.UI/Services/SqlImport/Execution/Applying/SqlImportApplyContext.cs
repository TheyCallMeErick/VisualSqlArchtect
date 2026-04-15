using System.Collections.ObjectModel;
using AkkornStudio.UI.Services.SqlImport.Build;
using AkkornStudio.UI.Services.SqlImport.Execution.Parsing;
using AkkornStudio.UI.ViewModels.Canvas;

namespace AkkornStudio.UI.Services.SqlImport.Execution.Applying;

internal sealed record SqlImportApplyContext(
    SqlImportParsedQuery Query,
    ImportBuildContext CoreContext,
    ObservableCollection<ImportReportItem> Report,
    CancellationToken CancellationToken
);

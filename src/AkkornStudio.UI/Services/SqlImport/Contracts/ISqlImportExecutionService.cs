using System.Collections.ObjectModel;
using AkkornStudio.UI.Services.SqlImport.Execution;
using AkkornStudio.UI.ViewModels.Canvas;

namespace AkkornStudio.UI.Services.SqlImport.Contracts;

public interface ISqlImportExecutionService
{
    SqlImportExecutionResult Execute(
        string sql,
        ObservableCollection<ImportReportItem> report,
        CancellationToken cancellationToken,
        bool roundTripEquivalenceCheckEnabled = false
    );
}

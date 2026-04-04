using System.Collections.ObjectModel;
using VisualSqlArchitect.UI.Services.SqlImport.Execution;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services.SqlImport.Contracts;

public interface ISqlImportExecutionService
{
    SqlImportExecutionResult Execute(
        string sql,
        ObservableCollection<ImportReportItem> report,
        CancellationToken cancellationToken
    );
}

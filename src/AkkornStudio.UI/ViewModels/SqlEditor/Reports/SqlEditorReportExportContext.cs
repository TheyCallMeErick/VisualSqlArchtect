using System.Collections.Generic;
using AkkornStudio.Core;

namespace AkkornStudio.UI.ViewModels;

public sealed record SqlEditorReportExportContext(
    string Sql,
    IReadOnlyList<string> SchemaColumns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> ResultRows,
    SqlEditorReportExecutionResult ExecutionResult,
    ConnectionConfig? Connection,
    string? ActiveFilePath,
    string TabTitle
);

using AkkornStudio.SqlImport.Diagnostics;
using AkkornStudio.SqlImport.Ids;
using AkkornStudio.SqlImport.IR.Expressions;
using AkkornStudio.SqlImport.IR.Metadata;
using AkkornStudio.SqlImport.Semantics.SymbolTable;

namespace AkkornStudio.SqlImport.IR;

public sealed record SqlToNodeIR(
    string IrVersion,
    string QueryId,
    string SourceHash,
    SqlImportDialect Dialect,
    IReadOnlyList<string> FeatureFlags,
    QueryExpr Query,
    SymbolTableModel SymbolTable,
    IReadOnlyList<SqlImportDiagnostic> Diagnostics,
    IrMetrics Metrics,
    IdGenerationMeta IdGenerationMeta
);

using DBWeaver.SqlImport.Diagnostics;
using DBWeaver.SqlImport.Ids;
using DBWeaver.SqlImport.IR.Expressions;
using DBWeaver.SqlImport.IR.Metadata;
using DBWeaver.SqlImport.Semantics.SymbolTable;

namespace DBWeaver.SqlImport.IR;

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

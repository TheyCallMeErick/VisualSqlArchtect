using DBWeaver.SqlImport.Contracts;
using DBWeaver.SqlImport.IR.Metadata;

namespace DBWeaver.SqlImport.IR.Sources;

public abstract record SourceExpr(
    string SourceId,
    string? Alias,
    SqlResolutionStatus ResolutionStatus,
    SqlIrNodeMetadata NodeMetadata
);

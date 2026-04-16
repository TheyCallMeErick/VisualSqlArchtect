using AkkornStudio.SqlImport.Contracts;
using AkkornStudio.SqlImport.IR.Metadata;

namespace AkkornStudio.SqlImport.IR.Sources;

public abstract record SourceExpr(
    string SourceId,
    string? Alias,
    SqlResolutionStatus ResolutionStatus,
    SqlIrNodeMetadata NodeMetadata
);

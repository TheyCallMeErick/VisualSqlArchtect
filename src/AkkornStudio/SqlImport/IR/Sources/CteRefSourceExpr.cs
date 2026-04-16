using AkkornStudio.SqlImport.Contracts;
using AkkornStudio.SqlImport.IR.Metadata;

namespace AkkornStudio.SqlImport.IR.Sources;

public sealed record CteRefSourceExpr(
    string SourceId,
    string Name,
    string? Alias,
    SqlResolutionStatus ResolutionStatus,
    SqlIrNodeMetadata NodeMetadata
) : SourceExpr(SourceId, Alias, ResolutionStatus, NodeMetadata);

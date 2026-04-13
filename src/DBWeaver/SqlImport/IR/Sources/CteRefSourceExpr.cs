using DBWeaver.SqlImport.Contracts;
using DBWeaver.SqlImport.IR.Metadata;

namespace DBWeaver.SqlImport.IR.Sources;

public sealed record CteRefSourceExpr(
    string SourceId,
    string Name,
    string? Alias,
    SqlResolutionStatus ResolutionStatus,
    SqlIrNodeMetadata NodeMetadata
) : SourceExpr(SourceId, Alias, ResolutionStatus, NodeMetadata);

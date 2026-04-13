using DBWeaver.SqlImport.Contracts;
using DBWeaver.SqlImport.IR.Metadata;

namespace DBWeaver.SqlImport.IR.Sources;

public sealed record TableRefSourceExpr(
    string SourceId,
    string? Database,
    string? Schema,
    string Table,
    string? Alias,
    SqlResolutionStatus ResolutionStatus,
    SqlIrNodeMetadata NodeMetadata
) : SourceExpr(SourceId, Alias, ResolutionStatus, NodeMetadata);

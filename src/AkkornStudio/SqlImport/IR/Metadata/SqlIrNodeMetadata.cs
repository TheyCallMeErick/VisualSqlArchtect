namespace AkkornStudio.SqlImport.IR.Metadata;

public sealed record SqlIrNodeMetadata(
    bool SyntheticNode,
    string? SyntheticOriginReason,
    IReadOnlyList<string> DerivedFromExprIds,
    IReadOnlyList<string> DerivedFromSpanHashes
);

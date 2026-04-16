namespace AkkornStudio.SqlImport.IR.Metadata;

public sealed record AliasMeta(
    string? OriginalAlias,
    string NormalizedAlias,
    string DisplayAlias,
    string NormalizationRule,
    IReadOnlyList<string> NormalizationLossFlags
);

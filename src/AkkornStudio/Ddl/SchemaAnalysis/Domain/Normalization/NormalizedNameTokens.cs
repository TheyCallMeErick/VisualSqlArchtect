namespace AkkornStudio.Ddl.SchemaAnalysis.Domain.Normalization;

public sealed record NormalizedNameTokens(
    IReadOnlyList<string> AllTokens,
    IReadOnlyList<string> StructuralTokens,
    IReadOnlyList<string> EntityTokens,
    string? PrincipalEntityToken
)
{
    public bool HasEntityTokens => EntityTokens.Count > 0;
}

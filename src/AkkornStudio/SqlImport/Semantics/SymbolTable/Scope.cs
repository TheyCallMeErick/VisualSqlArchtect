namespace AkkornStudio.SqlImport.Semantics.SymbolTable;

public sealed record Scope(
    string ScopeId,
    ScopeType ScopeType,
    string? ParentScopeId,
    IReadOnlyDictionary<string, IReadOnlyList<SourceSymbol>> SourceSymbols,
    IReadOnlyDictionary<string, IReadOnlyList<ProjectionSymbol>> ProjectionSymbols
);

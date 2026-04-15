namespace AkkornStudio.SqlImport.Semantics.SymbolTable;

public sealed record SourceSymbol(
    string SourceId,
    string Symbol,
    string NormalizedKey,
    string? Schema,
    string? Table,
    string? Alias
);

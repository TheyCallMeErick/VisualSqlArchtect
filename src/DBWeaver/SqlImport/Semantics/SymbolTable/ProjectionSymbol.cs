namespace DBWeaver.SqlImport.Semantics.SymbolTable;

public sealed record ProjectionSymbol(
    string SelectItemId,
    string Symbol,
    string NormalizedKey,
    int Ordinal
);

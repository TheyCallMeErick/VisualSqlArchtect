using DBWeaver.SqlImport.Contracts;

namespace DBWeaver.SqlImport.Semantics.Resolvers;

public sealed record SymbolResolutionResult(
    SqlResolutionStatus ResolutionStatus,
    string? BoundSourceId,
    string? BoundColumn,
    string? DiagnosticCode
);

using AkkornStudio.SqlImport.Contracts;

namespace AkkornStudio.SqlImport.Semantics.Resolvers;

public sealed record SymbolResolutionResult(
    SqlResolutionStatus ResolutionStatus,
    string? BoundSourceId,
    string? BoundColumn,
    string? DiagnosticCode
);

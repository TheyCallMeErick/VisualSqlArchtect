using AkkornStudio.SqlImport.Contracts;
using AkkornStudio.SqlImport.Diagnostics;

namespace AkkornStudio.SqlImport.Outcome;

public sealed record ImportOutcome(
    ImportOutcomeStatus Status,
    ImportEquivalenceClass EquivalenceClass,
    bool HasDegradedGraph,
    IReadOnlyList<SqlImportDiagnostic> BlockingDiagnostics,
    IReadOnlyList<SqlImportDiagnostic> NonBlockingDiagnostics
);

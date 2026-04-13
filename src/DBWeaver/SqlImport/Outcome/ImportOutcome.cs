using DBWeaver.SqlImport.Contracts;
using DBWeaver.SqlImport.Diagnostics;

namespace DBWeaver.SqlImport.Outcome;

public sealed record ImportOutcome(
    ImportOutcomeStatus Status,
    ImportEquivalenceClass EquivalenceClass,
    bool HasDegradedGraph,
    IReadOnlyList<SqlImportDiagnostic> BlockingDiagnostics,
    IReadOnlyList<SqlImportDiagnostic> NonBlockingDiagnostics
);

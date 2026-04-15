namespace DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;

public sealed record SchemaSuggestion(
    string SuggestionId,
    string Title,
    string Description,
    double Confidence,
    IReadOnlyList<SqlFixCandidate> SqlCandidates
);

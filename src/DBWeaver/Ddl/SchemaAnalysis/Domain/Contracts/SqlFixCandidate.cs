using DBWeaver.Core;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;

public sealed record SqlFixCandidate(
    string CandidateId,
    DatabaseProvider Provider,
    string Title,
    string Sql,
    IReadOnlyList<string> PreconditionsSql,
    SqlCandidateSafety Safety,
    CandidateVisibility Visibility,
    bool IsAutoApplicable,
    IReadOnlyList<string> Notes
);

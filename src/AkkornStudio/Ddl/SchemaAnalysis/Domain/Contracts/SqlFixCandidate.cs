using AkkornStudio.Core;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;

namespace AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;

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

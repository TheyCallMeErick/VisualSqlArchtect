using DBWeaver.Core;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Caching;

public sealed record SchemaAnalysisCacheKey(
    string MetadataFingerprint,
    string ProfileContentHash,
    DatabaseProvider Provider,
    int SpecVersion
);

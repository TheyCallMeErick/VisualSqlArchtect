using AkkornStudio.Core;

namespace AkkornStudio.Ddl.SchemaAnalysis.Application.Caching;

public sealed record SchemaAnalysisCacheKey(
    string MetadataFingerprint,
    string ProfileContentHash,
    DatabaseProvider Provider,
    int SpecVersion
);

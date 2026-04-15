using AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;

namespace AkkornStudio.Ddl.SchemaAnalysis.Application.Caching;

public interface ISchemaAnalysisCache
{
    bool TryGet(SchemaAnalysisCacheKey key, out SchemaAnalysisResult? cachedResult);

    void Set(SchemaAnalysisCacheKey key, SchemaAnalysisResult result, TimeSpan ttl);
}

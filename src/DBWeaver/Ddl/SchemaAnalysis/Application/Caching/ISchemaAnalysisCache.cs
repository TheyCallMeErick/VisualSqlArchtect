using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Caching;

public interface ISchemaAnalysisCache
{
    bool TryGet(SchemaAnalysisCacheKey key, out SchemaAnalysisResult? cachedResult);

    void Set(SchemaAnalysisCacheKey key, SchemaAnalysisResult result, TimeSpan ttl);
}

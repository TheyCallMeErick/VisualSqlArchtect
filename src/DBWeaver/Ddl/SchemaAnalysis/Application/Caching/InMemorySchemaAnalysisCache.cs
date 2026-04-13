using System.Collections.Concurrent;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Caching;

public sealed class InMemorySchemaAnalysisCache : ISchemaAnalysisCache
{
    private readonly ConcurrentDictionary<SchemaAnalysisCacheKey, CacheEntry> _entries = new();

    public bool TryGet(SchemaAnalysisCacheKey key, out SchemaAnalysisResult? cachedResult)
    {
        cachedResult = null;

        if (!_entries.TryGetValue(key, out CacheEntry? entry))
        {
            return false;
        }

        if (entry.ExpiresAtUtc < DateTimeOffset.UtcNow)
        {
            _entries.TryRemove(key, out _);
            return false;
        }

        cachedResult = entry.Result;
        return true;
    }

    public void Set(SchemaAnalysisCacheKey key, SchemaAnalysisResult result, TimeSpan ttl)
    {
        _entries[key] = new CacheEntry(result, DateTimeOffset.UtcNow.Add(ttl));
    }

    private sealed record CacheEntry(SchemaAnalysisResult Result, DateTimeOffset ExpiresAtUtc);
}

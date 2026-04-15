using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Processing;

public static class SchemaEvidenceFactory
{
    public static SchemaEvidence MetadataFact(string key, string value, double weight, string? sourcePath = null) =>
        Create(EvidenceKind.MetadataFact, key, value, weight, sourcePath);

    public static SchemaEvidence NamingMatch(string key, string value, double weight, string? sourcePath = null) =>
        Create(EvidenceKind.NamingMatch, key, value, weight, sourcePath);

    public static SchemaEvidence TypeCompatibility(string key, string value, double weight, string? sourcePath = null) =>
        Create(EvidenceKind.TypeCompatibility, key, value, weight, sourcePath);

    public static SchemaEvidence ConstraintTopology(string key, string value, double weight, string? sourcePath = null) =>
        Create(EvidenceKind.ConstraintTopology, key, value, weight, sourcePath);

    public static SchemaEvidence PolicyRequirement(string key, string value, double weight, string? sourcePath = null) =>
        Create(EvidenceKind.PolicyRequirement, key, value, weight, sourcePath);

    public static SchemaEvidence Ambiguity(string key, string value, double weight, string? sourcePath = null) =>
        Create(EvidenceKind.Ambiguity, key, value, weight, sourcePath);

    public static SchemaEvidence ProviderLimitation(string key, string value, double weight, string? sourcePath = null) =>
        Create(EvidenceKind.ProviderLimitation, key, value, weight, sourcePath);

    public static SchemaEvidence ThresholdDecision(string key, string value, double weight, string? sourcePath = null) =>
        Create(EvidenceKind.ThresholdDecision, key, value, weight, sourcePath);

    public static SchemaEvidence ExecutionBoundary(string key, string value, double weight, string? sourcePath = null) =>
        Create(EvidenceKind.ExecutionBoundary, key, value, weight, sourcePath);

    public static SchemaEvidence Create(
        EvidenceKind kind,
        string key,
        string value,
        double weight,
        string? sourcePath = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        double normalizedWeight = Math.Round(Math.Clamp(weight, 0d, 1d), 4, MidpointRounding.ToEven);
        return new SchemaEvidence(kind, key, value, normalizedWeight, sourcePath);
    }
}

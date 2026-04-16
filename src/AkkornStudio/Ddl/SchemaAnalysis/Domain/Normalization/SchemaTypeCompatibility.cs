namespace AkkornStudio.Ddl.SchemaAnalysis.Domain.Normalization;

public sealed record SchemaTypeCompatibility(
    string LeftNormalizedType,
    string RightNormalizedType,
    SchemaCanonicalTypeCategory LeftCategory,
    SchemaCanonicalTypeCategory RightCategory,
    SchemaTypeCompatibilityLevel CompatibilityLevel
);

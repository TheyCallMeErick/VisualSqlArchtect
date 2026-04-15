using AkkornStudio.Ddl.SchemaAnalysis.Application.Indexing;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;
using AkkornStudio.Metadata;

namespace AkkornStudio.Ddl.SchemaAnalysis.Application.Rules;

public sealed record SchemaAnalysisExecutionContext(
    DbMetadata Metadata,
    SchemaAnalysisProfile Profile,
    SchemaMetadataIndexSnapshot Indices,
    string MetadataFingerprint,
    string ProfileContentHash
);

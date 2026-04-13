using DBWeaver.Ddl.SchemaAnalysis.Application.Indexing;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Metadata;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Rules;

public sealed record SchemaAnalysisExecutionContext(
    DbMetadata Metadata,
    SchemaAnalysisProfile Profile,
    SchemaMetadataIndexSnapshot Indices,
    string MetadataFingerprint,
    string ProfileContentHash
);

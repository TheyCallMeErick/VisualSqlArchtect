using DBWeaver.Ddl.SchemaAnalysis.Domain.Normalization;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Indexing;

public sealed record SchemaNormalizedNameIndexEntry(
    string Key,
    SchemaTargetType TargetType,
    string? SchemaName,
    string? TableName,
    string? ColumnName,
    string? ConstraintName,
    NormalizedNameTokens Tokens
);

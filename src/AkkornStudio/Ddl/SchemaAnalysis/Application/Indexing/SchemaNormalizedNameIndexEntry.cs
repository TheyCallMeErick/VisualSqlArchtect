using AkkornStudio.Ddl.SchemaAnalysis.Domain.Normalization;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;

namespace AkkornStudio.Ddl.SchemaAnalysis.Application.Indexing;

public sealed record SchemaNormalizedNameIndexEntry(
    string Key,
    SchemaTargetType TargetType,
    string? SchemaName,
    string? TableName,
    string? ColumnName,
    string? ConstraintName,
    NormalizedNameTokens Tokens
);

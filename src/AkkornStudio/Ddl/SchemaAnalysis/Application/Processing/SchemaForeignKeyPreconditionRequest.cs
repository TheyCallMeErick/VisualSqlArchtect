using AkkornStudio.Core;

namespace AkkornStudio.Ddl.SchemaAnalysis.Application.Processing;

public sealed record SchemaForeignKeyPreconditionRequest(
    DatabaseProvider Provider,
    string? SchemaName,
    string ChildTable,
    IReadOnlyList<string> ChildColumns,
    string ParentTable,
    IReadOnlyList<string> ParentColumns,
    string ConstraintName
);

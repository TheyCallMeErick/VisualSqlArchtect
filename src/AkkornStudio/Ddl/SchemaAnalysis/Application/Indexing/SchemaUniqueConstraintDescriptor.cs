namespace AkkornStudio.Ddl.SchemaAnalysis.Application.Indexing;

public sealed record SchemaUniqueConstraintDescriptor(
    string ConstraintName,
    IReadOnlyList<string> Columns
);

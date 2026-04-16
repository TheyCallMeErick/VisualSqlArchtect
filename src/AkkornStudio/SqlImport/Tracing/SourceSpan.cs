namespace AkkornStudio.SqlImport.Tracing;

public sealed record SourceSpan(
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn,
    string SourceFragmentHash
);

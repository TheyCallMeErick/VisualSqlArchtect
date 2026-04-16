using AkkornStudio.SqlImport.IR.Expressions;
using AkkornStudio.SqlImport.IR.Metadata;
using AkkornStudio.SqlImport.Tracing;

namespace AkkornStudio.SqlImport.IR;

public sealed record SelectItemExpr(
    string SelectItemId,
    SqlExpression Expression,
    AliasMeta AliasMeta,
    int Ordinal,
    SqlImportSemanticType SemanticType,
    SourceSpan SourceSpan,
    SqlIrNodeMetadata NodeMetadata
);

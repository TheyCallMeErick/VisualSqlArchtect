using DBWeaver.SqlImport.IR.Expressions;
using DBWeaver.SqlImport.IR.Metadata;
using DBWeaver.SqlImport.Tracing;

namespace DBWeaver.SqlImport.IR;

public sealed record SelectItemExpr(
    string SelectItemId,
    SqlExpression Expression,
    AliasMeta AliasMeta,
    int Ordinal,
    SqlImportSemanticType SemanticType,
    SourceSpan SourceSpan,
    SqlIrNodeMetadata NodeMetadata
);

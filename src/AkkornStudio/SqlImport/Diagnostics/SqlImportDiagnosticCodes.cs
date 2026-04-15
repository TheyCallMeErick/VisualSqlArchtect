namespace AkkornStudio.SqlImport.Diagnostics;

public static class SqlImportDiagnosticCodes
{
    public const string ParseFatal = "SQLIMP_0001_PARSE_FATAL";
    public const string AstUnsupported = "SQLIMP_0002_AST_UNSUPPORTED";
    public const string AliasNormalizationLoss = "SQLIMP_0101_ALIAS_NORMALIZATION_LOSS";
    public const string AliasNormalizationCollision = "SQLIMP_0102_ALIAS_NORMALIZATION_COLLISION";
    public const string ColumnAmbiguous = "SQLIMP_0201_COLUMN_AMBIGUOUS";
    public const string ColumnUnresolved = "SQLIMP_0202_COLUMN_UNRESOLVED";
    public const string SetOperandPrecedenceAmbiguous = "SQLIMP_0301_SET_OPERAND_PRECEDENCE_AMBIGUOUS";
    public const string SetOperandArityMismatch = "SQLIMP_0302_SET_OPERAND_ARITY_MISMATCH";
    public const string SetOperandSemanticMismatch = "SQLIMP_0303_SET_OPERAND_SEMANTIC_MISMATCH";
    public const string FunctionGenericPreserved = "SQLIMP_0401_FUNCTION_GENERIC_PRESERVED";
    public const string FunctionUnsupported = "SQLIMP_0402_FUNCTION_UNSUPPORTED";
    public const string FunctionGenericForbiddenContext = "SQLIMP_0403_FUNCTION_GENERIC_FORBIDDEN_CONTEXT";
    public const string FallbackRegexUsed = "SQLIMP_0501_FALLBACK_REGEX_USED";
    public const string TypeInferenceFallback = "SQLIMP_0601_TYPE_INFERENCE_FALLBACK";
    public const string ProjectionDroppedBlocked = "SQLIMP_0701_PROJECTION_DROPPED_BLOCKED";
    public const string ValueMapLegacyCompat = "SQLIMP_0801_VALUEMAP_LEGACY_COMPAT";
    public const string ValueMapStructInvalid = "SQLIMP_0802_VALUEMAP_STRUCT_INVALID";
    public const string StarPreservedMissingMetadata = "SQLIMP_0851_STAR_PRESERVED_MISSING_METADATA";
    public const string StarAliasUnresolved = "SQLIMP_0852_STAR_ALIAS_UNRESOLVED";
    public const string RoundtripNotEquivalent = "SQLIMP_0901_ROUNDTRIP_NOT_EQUIVALENT";
    public const string RoundtripCheckDisabled = "SQLIMP_0902_ROUNDTRIP_CHECK_DISABLED";
}

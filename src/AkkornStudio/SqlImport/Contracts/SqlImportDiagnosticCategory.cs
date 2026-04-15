namespace AkkornStudio.SqlImport.Contracts;

public enum SqlImportDiagnosticCategory
{
    FatalError,
    PartialImport,
    Warning,
    UnsupportedFeature,
    FallbackActivated,
    AmbiguityUnresolved,
    NormalizationLoss,
}

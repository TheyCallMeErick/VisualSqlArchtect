namespace AkkornStudio.UI.Services.SqlImport;

public sealed class SqlImportFeatureFlags
{
    public bool AstIrPrimary { get; set; }

    public bool RegexFallbackEnabled { get; set; } = true;

    public bool ValueMapGraphFirst { get; set; }

    public bool RoundTripEquivalenceCheck { get; set; }

    public bool UseAstParser
    {
        get => AstIrPrimary;
        set => AstIrPrimary = value;
    }
}

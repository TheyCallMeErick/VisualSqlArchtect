namespace DBWeaver.SqlImport.Contracts;

public enum SqlImportDiagnosticAction
{
    Abort,
    ContinuePartial,
    Fallback,
}

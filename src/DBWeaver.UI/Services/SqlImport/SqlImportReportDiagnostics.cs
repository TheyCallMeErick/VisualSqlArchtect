namespace DBWeaver.UI.Services.SqlImport;

public static class SqlImportReportDiagnostics
{
    public static string WithCode(string code, string label)
    {
        return $"{code} {label}";
    }
}

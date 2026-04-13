namespace DBWeaver.SqlImport.Contracts;

public enum SqlResolutionStatus
{
    Resolved,
    Partial,
    Ambiguous,
    Unresolved,
}

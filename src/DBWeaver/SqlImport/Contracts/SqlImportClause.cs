namespace DBWeaver.SqlImport.Contracts;

public enum SqlImportClause
{
    Select,
    From,
    Join,
    Where,
    GroupBy,
    Having,
    OrderBy,
    Limit,
    Function,
    ValueMap,
    Star,
    Unknown,
}

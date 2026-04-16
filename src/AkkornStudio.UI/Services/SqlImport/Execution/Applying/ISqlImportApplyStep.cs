namespace AkkornStudio.UI.Services.SqlImport.Execution.Applying;

internal interface ISqlImportApplyStep
{
    SqlImportApplyResult Apply(SqlImportApplyContext context);
}

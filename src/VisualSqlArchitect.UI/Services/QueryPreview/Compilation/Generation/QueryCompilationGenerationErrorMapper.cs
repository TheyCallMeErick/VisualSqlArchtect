namespace VisualSqlArchitect.UI.Services.QueryPreview;

internal sealed class QueryCompilationGenerationErrorMapper
{
    public IEnumerable<string> Map(Exception ex)
    {
        if (ex is InvalidOperationException && ex.Message.Contains("Cycle detected between CTE definitions", StringComparison.OrdinalIgnoreCase))
        {
            yield return ex.Message;
            yield return "CTE cycle detected. Remove circular CTE dependencies or refactor with a base CTE plus recursive CTE.";
            yield break;
        }

        if (ex is InvalidOperationException && ex.Message.Contains("references itself but is not marked recursive", StringComparison.OrdinalIgnoreCase))
        {
            yield return ex.Message;
            yield return "CTE self-reference requires the 'recursive' flag enabled on the CTE Definition node.";
            yield break;
        }

        if (ex is NotSupportedException && ex.Message.Contains("requires 'value' input", StringComparison.OrdinalIgnoreCase))
        {
            yield return ex.Message;
            yield return "Window function is missing required 'value' input. Connect a value pin for this function type.";
            yield break;
        }

        yield return ex.Message;
    }
}



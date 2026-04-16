namespace AkkornStudio.Ddl.SchemaAnalysis.Domain.Normalization;

public static class SchemaTokenSingularizer
{
    public static string Singularize(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (token.Length == 0)
        {
            return token;
        }

        if (token.EndsWith("ies", StringComparison.Ordinal) && token.Length > 3)
        {
            return token[..^3] + "y";
        }

        if (
            token.EndsWith("sses", StringComparison.Ordinal)
            || token.EndsWith("shes", StringComparison.Ordinal)
            || token.EndsWith("ches", StringComparison.Ordinal)
            || token.EndsWith("xes", StringComparison.Ordinal)
            || token.EndsWith("zes", StringComparison.Ordinal)
        )
        {
            return token[..^2];
        }

        if (
            token.EndsWith("ses", StringComparison.Ordinal)
            && token.Length > 3
            && token[^4] != 's'
        )
        {
            return token[..^2];
        }

        if (
            token.EndsWith('s')
            && token.Length > 1
            && !token.EndsWith("ss", StringComparison.Ordinal)
            && !token.EndsWith("us", StringComparison.Ordinal)
        )
        {
            return token[..^1];
        }

        return token;
    }
}

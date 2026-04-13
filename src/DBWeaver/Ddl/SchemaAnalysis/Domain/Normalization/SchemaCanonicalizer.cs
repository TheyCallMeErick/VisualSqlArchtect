using DBWeaver.Core;

namespace DBWeaver.Ddl.SchemaAnalysis.Domain.Normalization;

public static class SchemaCanonicalizer
{
    public static string? Normalize(DatabaseProvider provider, string? schema)
    {
        return provider switch
        {
            DatabaseProvider.Postgres => NormalizeToDefault(schema, "public"),
            DatabaseProvider.SqlServer => NormalizeToDefault(schema, "dbo"),
            DatabaseProvider.MySql => null,
            DatabaseProvider.SQLite => NormalizeToDefault(schema, "main"),
            _ => NormalizeToDefault(schema, defaultSchema: null),
        };
    }

    public static bool AreEquivalent(DatabaseProvider provider, string? leftSchema, string? rightSchema)
    {
        string? leftCanonical = Normalize(provider, leftSchema);
        string? rightCanonical = Normalize(provider, rightSchema);

        return string.Equals(leftCanonical, rightCanonical, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeToDefault(string? schema, string? defaultSchema)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            return defaultSchema;
        }

        return schema.Trim();
    }
}

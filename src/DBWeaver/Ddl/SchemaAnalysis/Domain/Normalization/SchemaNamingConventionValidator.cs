using System.Text.RegularExpressions;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Ddl.SchemaAnalysis.Domain.Normalization;

public sealed partial class SchemaNamingConventionValidator
{
    public bool IsValid(string rawName, NamingConvention namingConvention)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawName);

        if (char.IsDigit(rawName[0]))
        {
            return false;
        }

        return namingConvention switch
        {
            NamingConvention.SnakeCase => SnakeCaseRegex().IsMatch(rawName),
            NamingConvention.CamelCase => CamelCaseRegex().IsMatch(rawName),
            NamingConvention.PascalCase => PascalCaseRegex().IsMatch(rawName),
            NamingConvention.KebabCase => KebabCaseRegex().IsMatch(rawName),
            NamingConvention.MixedAllowed => MixedAllowedRegex().IsMatch(rawName),
            _ => false,
        };
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SnakeCaseRegex();

    [GeneratedRegex("^[a-z]+(?:[A-Z][a-z0-9]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CamelCaseRegex();

    [GeneratedRegex("^[A-Z][a-z0-9]*(?:[A-Z][a-z0-9]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex PascalCaseRegex();

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex KebabCaseRegex();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex MixedAllowedRegex();
}

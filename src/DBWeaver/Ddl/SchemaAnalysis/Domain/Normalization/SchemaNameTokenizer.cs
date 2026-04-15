using System.Globalization;
using System.Text;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;

namespace DBWeaver.Ddl.SchemaAnalysis.Domain.Normalization;

public sealed class SchemaNameTokenizer
{
    private readonly SchemaTokenEquivalenceResolver _equivalenceResolver = new();

    private static readonly HashSet<string> StructuralTokenSet = new(StringComparer.Ordinal)
    {
        "id",
        "fk",
        "ref",
        "code",
    };

    public NormalizedNameTokens Tokenize(string rawName, SchemaAnalysisProfile profile)
    {
        ArgumentNullException.ThrowIfNull(rawName);
        ArgumentNullException.ThrowIfNull(profile);

        SchemaTokenEquivalenceResolution equivalenceResolution = _equivalenceResolver.Resolve(profile);

        IReadOnlyList<string> tokens = BuildBaseTokens(rawName)
            .Select(SchemaTokenSingularizer.Singularize)
            .Select(equivalenceResolution.NormalizeToken)
            .Where(token => token.Length > 0)
            .ToList();

        IReadOnlyList<string> structuralTokens = tokens
            .Where(token => StructuralTokenSet.Contains(token))
            .ToList();

        IReadOnlyList<string> entityTokens = tokens
            .Where(token => !StructuralTokenSet.Contains(token))
            .ToList();

        string? principalEntityToken = entityTokens.FirstOrDefault();

        return new NormalizedNameTokens(tokens, structuralTokens, entityTokens, principalEntityToken);
    }

    public NormalizedNameTokens Tokenize(string rawName)
    {
        ArgumentNullException.ThrowIfNull(rawName);
        IReadOnlyList<string> tokens = BuildBaseTokens(rawName)
            .Select(SchemaTokenSingularizer.Singularize)
            .Where(token => token.Length > 0)
            .ToList();

        IReadOnlyList<string> structuralTokens = tokens
            .Where(token => StructuralTokenSet.Contains(token))
            .ToList();

        IReadOnlyList<string> entityTokens = tokens
            .Where(token => !StructuralTokenSet.Contains(token))
            .ToList();

        return new NormalizedNameTokens(tokens, structuralTokens, entityTokens, entityTokens.FirstOrDefault());
    }

    private static IReadOnlyList<string> BuildBaseTokens(string rawName)
    {
        string noDiacritics = RemoveDiacritics(rawName);
        string withCamelBoundaries = SplitCamelAndPascalBoundaries(noDiacritics);
        string lowered = withCamelBoundaries.ToLowerInvariant();
        string onlyAsciiAlphaNumeric = ReplaceNonAsciiAlphaNumericWithSpace(lowered);

        return onlyAsciiAlphaNumeric
            .Split([' ', '_', '-', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length > 0)
            .ToList();
    }

    private static string RemoveDiacritics(string value)
    {
        string normalized = value.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(normalized.Length);

        foreach (char character in normalized)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string SplitCamelAndPascalBoundaries(string value)
    {
        if (value.Length <= 1)
        {
            return value;
        }

        StringBuilder builder = new(value.Length + 8);
        builder.Append(value[0]);

        for (int index = 1; index < value.Length; index++)
        {
            char previous = value[index - 1];
            char current = value[index];

            if (char.IsLower(previous) && char.IsUpper(current))
            {
                builder.Append(' ');
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static string ReplaceNonAsciiAlphaNumericWithSpace(string value)
    {
        StringBuilder builder = new(value.Length);
        foreach (char character in value)
        {
            if (character is >= 'a' and <= 'z' || character is >= '0' and <= '9')
            {
                builder.Append(character);
            }
            else
            {
                builder.Append(' ');
            }
        }

        return builder.ToString();
    }
}

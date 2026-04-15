using System.Text;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Normalization;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Processing;

public sealed class SchemaForeignKeyConstraintNameGenerator
{
    private const int MaxLength = 63;
    private const int TruncatedPrefixLength = 55;
    private const int HashSuffixLength = 7;

    public string? Generate(
        string childTable,
        string childColumn,
        string parentTable,
        string parentColumn,
        IReadOnlySet<string> existingConstraintNames
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(childTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(childColumn);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentColumn);
        ArgumentNullException.ThrowIfNull(existingConstraintNames);

        HashSet<string> existingNames = existingConstraintNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string canonicalBaseName = BuildCanonicalBaseName(childTable, childColumn, parentTable, parentColumn);
        string normalizedBaseName = ApplyLengthLimit(canonicalBaseName);

        if (!existingNames.Contains(normalizedBaseName))
        {
            return normalizedBaseName;
        }

        for (int version = 2; version <= 99; version++)
        {
            string suffix = $"_v{version}";
            string candidate = AppendVersionSuffix(normalizedBaseName, suffix);
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string BuildCanonicalBaseName(
        string childTable,
        string childColumn,
        string parentTable,
        string parentColumn
    )
    {
        return string.Join(
            string.Empty,
            "fk_",
            NormalizeSegment(childTable),
            "_",
            NormalizeSegment(childColumn),
            "__",
            NormalizeSegment(parentTable),
            "_",
            NormalizeSegment(parentColumn)
        );
    }

    private static string NormalizeSegment(string rawSegment)
    {
        string normalized = rawSegment.Trim().ToLowerInvariant();
        StringBuilder builder = new(normalized.Length);
        bool previousWasUnderscore = false;

        foreach (char character in normalized)
        {
            bool isAsciiAlphaNumeric = character is >= 'a' and <= 'z' || character is >= '0' and <= '9';
            if (isAsciiAlphaNumeric)
            {
                builder.Append(character);
                previousWasUnderscore = false;
                continue;
            }

            if (!previousWasUnderscore)
            {
                builder.Append('_');
                previousWasUnderscore = true;
            }
        }

        string collapsed = builder.ToString().Trim('_');
        return collapsed.Length == 0 ? "x" : collapsed;
    }

    private static string ApplyLengthLimit(string canonicalBaseName)
    {
        if (canonicalBaseName.Length <= MaxLength)
        {
            return canonicalBaseName;
        }

        string hash = SchemaIssueTextNormalizer
            .ComputeSha256Hex(SchemaIssueTextNormalizer.NormalizeForHash(canonicalBaseName))
            .Substring(0, HashSuffixLength);
        string prefix = canonicalBaseName.Substring(0, TruncatedPrefixLength).TrimEnd('_');
        prefix = prefix.Length == 0 ? canonicalBaseName.Substring(0, Math.Min(TruncatedPrefixLength, canonicalBaseName.Length)) : prefix;
        return $"{prefix}_{hash}";
    }

    private static string AppendVersionSuffix(string baseName, string suffix)
    {
        if (baseName.Length + suffix.Length <= MaxLength)
        {
            return baseName + suffix;
        }

        int allowedBaseLength = MaxLength - suffix.Length;
        string trimmedBase = baseName[..allowedBaseLength].TrimEnd('_');
        if (trimmedBase.Length == 0)
        {
            trimmedBase = baseName[..allowedBaseLength];
        }

        return trimmedBase + suffix;
    }
}

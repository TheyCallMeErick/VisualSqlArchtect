using System.Text.RegularExpressions;
using DBWeaver.Core;

namespace DBWeaver.Ddl.SchemaAnalysis.Domain.Normalization;

public sealed partial class SchemaTypeCompatibilityResolver
{
    public SchemaCanonicalTypeCategory GetCanonicalCategory(
        string rawType,
        DatabaseProvider provider
    )
    {
        ParsedTypeDescriptor descriptor = Parse(rawType, provider);
        return descriptor.Category;
    }

    public SchemaTypeCompatibility GetCompatibility(
        string leftRawType,
        string rightRawType,
        DatabaseProvider provider
    )
    {
        ParsedTypeDescriptor left = Parse(leftRawType, provider);
        ParsedTypeDescriptor right = Parse(rightRawType, provider);

        SchemaTypeCompatibilityLevel compatibilityLevel = ResolveCompatibilityLevel(
            left,
            right,
            provider
        );

        return new SchemaTypeCompatibility(
            LeftNormalizedType: left.NormalizedType,
            RightNormalizedType: right.NormalizedType,
            LeftCategory: left.Category,
            RightCategory: right.Category,
            CompatibilityLevel: compatibilityLevel
        );
    }

    private static SchemaTypeCompatibilityLevel ResolveCompatibilityLevel(
        ParsedTypeDescriptor left,
        ParsedTypeDescriptor right,
        DatabaseProvider provider
    )
    {
        if (string.Equals(left.NormalizedType, right.NormalizedType, StringComparison.Ordinal))
        {
            return SchemaTypeCompatibilityLevel.Exact;
        }

        if (IsWeakCompatibility(left, right, provider))
        {
            return SchemaTypeCompatibilityLevel.SemanticWeak;
        }

        if (left.Category == right.Category && left.Category != SchemaCanonicalTypeCategory.Other)
        {
            return SchemaTypeCompatibilityLevel.SemanticStrong;
        }

        return SchemaTypeCompatibilityLevel.Incompatible;
    }

    private static bool IsWeakCompatibility(
        ParsedTypeDescriptor left,
        ParsedTypeDescriptor right,
        DatabaseProvider provider
    )
    {
        if (IsUuidStringWeakPair(left, right))
        {
            return true;
        }

        if (IsNumericIntegerWeakPair(left, right))
        {
            return true;
        }

        if (provider == DatabaseProvider.MySql && IsMySqlTinyIntOneMixedWithInteger(left, right))
        {
            return false;
        }

        if (provider == DatabaseProvider.SqlServer && IsSqlServerBitMixedWithInteger(left, right))
        {
            return false;
        }

        return false;
    }

    private static bool IsUuidStringWeakPair(ParsedTypeDescriptor left, ParsedTypeDescriptor right)
    {
        return (left.Category, right.Category) switch
        {
            (SchemaCanonicalTypeCategory.Guid, SchemaCanonicalTypeCategory.String) => IsString36(left, right),
            (SchemaCanonicalTypeCategory.String, SchemaCanonicalTypeCategory.Guid) => IsString36(right, left),
            _ => false,
        };
    }

    private static bool IsString36(ParsedTypeDescriptor guid, ParsedTypeDescriptor text)
    {
        _ = guid;
        return (text.BaseType is "varchar" or "char") && text.FirstArgument == 36;
    }

    private static bool IsNumericIntegerWeakPair(ParsedTypeDescriptor left, ParsedTypeDescriptor right)
    {
        return (left.Category, right.Category) switch
        {
            (SchemaCanonicalTypeCategory.Decimal, SchemaCanonicalTypeCategory.Integer) => IsDecimalScaleZero(left),
            (SchemaCanonicalTypeCategory.Integer, SchemaCanonicalTypeCategory.Decimal) => IsDecimalScaleZero(right),
            _ => false,
        };
    }

    private static bool IsDecimalScaleZero(ParsedTypeDescriptor decimalType)
    {
        return decimalType.BaseType == "numeric" && decimalType.SecondArgument == 0;
    }

    private static bool IsMySqlTinyIntOneMixedWithInteger(
        ParsedTypeDescriptor left,
        ParsedTypeDescriptor right
    )
    {
        return (left.IsMySqlTinyIntOneBooleanAlias, right.Category) switch
        {
            (true, SchemaCanonicalTypeCategory.Integer) => true,
            _ when right.IsMySqlTinyIntOneBooleanAlias && left.Category == SchemaCanonicalTypeCategory.Integer => true,
            _ => false,
        };
    }

    private static bool IsSqlServerBitMixedWithInteger(
        ParsedTypeDescriptor left,
        ParsedTypeDescriptor right
    )
    {
        return (left.BaseType, right.Category) switch
        {
            ("bit", SchemaCanonicalTypeCategory.Integer) => true,
            _ when right.BaseType == "bit" && left.Category == SchemaCanonicalTypeCategory.Integer => true,
            _ => false,
        };
    }

    private static ParsedTypeDescriptor Parse(string rawType, DatabaseProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawType);

        string compact = CollapseWhitespace(rawType).ToLowerInvariant();
        Match match = TypePattern().Match(compact);
        string baseType = match.Success ? match.Groups["name"].Value : compact;

        int? firstArgument = TryParseNullableInt(match, "arg1");
        int? secondArgument = TryParseNullableInt(match, "arg2");

        SchemaCanonicalTypeCategory category = ResolveCategory(
            baseType,
            firstArgument,
            provider
        );

        string normalizedType = NormalizeType(baseType, firstArgument, secondArgument, provider);
        bool isMySqlTinyIntOneBooleanAlias =
            provider == DatabaseProvider.MySql && baseType == "tinyint" && firstArgument == 1;

        return new ParsedTypeDescriptor(
            RawType: rawType,
            BaseType: baseType,
            NormalizedType: normalizedType,
            Category: category,
            FirstArgument: firstArgument,
            SecondArgument: secondArgument,
            IsMySqlTinyIntOneBooleanAlias: isMySqlTinyIntOneBooleanAlias
        );
    }

    private static SchemaCanonicalTypeCategory ResolveCategory(
        string baseType,
        int? firstArgument,
        DatabaseProvider provider
    )
    {
        return baseType switch
        {
            "int" or "integer" or "bigint" or "smallint" or "serial" or "bigserial"
                => SchemaCanonicalTypeCategory.Integer,
            "tinyint" when provider == DatabaseProvider.MySql && firstArgument == 1
                => SchemaCanonicalTypeCategory.Boolean,
            "tinyint" => SchemaCanonicalTypeCategory.Integer,
            "decimal" or "numeric" or "money" or "float" or "double" or "real"
                => SchemaCanonicalTypeCategory.Decimal,
            "char" or "varchar" or "nvarchar" or "nchar" or "text" or "longtext"
                => SchemaCanonicalTypeCategory.String,
            "date" or "time" or "timestamp" or "datetime" or "datetime2"
                => SchemaCanonicalTypeCategory.DateTime,
            "bool" or "boolean" or "bit" => SchemaCanonicalTypeCategory.Boolean,
            "uuid" or "uniqueidentifier" => SchemaCanonicalTypeCategory.Guid,
            "blob" or "varbinary" or "binary" or "bytea" => SchemaCanonicalTypeCategory.Binary,
            "json" or "jsonb" or "xml" => SchemaCanonicalTypeCategory.JsonXml,
            _ => SchemaCanonicalTypeCategory.Other,
        };
    }

    private static string NormalizeType(
        string baseType,
        int? firstArgument,
        int? secondArgument,
        DatabaseProvider provider
    )
    {
        return baseType switch
        {
            "int" or "integer" => "integer",
            "bigint" => "bigint",
            "smallint" => "smallint",
            "serial" => "serial",
            "bigserial" => "bigserial",
            "tinyint" when provider == DatabaseProvider.MySql && firstArgument == 1 => "boolean",
            "tinyint" => "tinyint",
            "decimal" => BuildNormalized(baseType, firstArgument, secondArgument),
            "numeric" => BuildNormalized(baseType, firstArgument, secondArgument),
            "money" => "money",
            "float" => "float",
            "double" => "double",
            "real" => "real",
            "char" => BuildNormalized(baseType, firstArgument, secondArgument),
            "varchar" => BuildNormalized(baseType, firstArgument, secondArgument),
            "nvarchar" => BuildNormalized(baseType, firstArgument, secondArgument),
            "nchar" => BuildNormalized(baseType, firstArgument, secondArgument),
            "text" => "text",
            "longtext" => "longtext",
            "date" => "date",
            "time" => "time",
            "timestamp" => "timestamp",
            "datetime" => "datetime",
            "datetime2" => "datetime2",
            "bool" or "boolean" => "boolean",
            "bit" => "bit",
            "uuid" => "uuid",
            "uniqueidentifier" => "uuid",
            "blob" => "blob",
            "varbinary" => BuildNormalized(baseType, firstArgument, secondArgument),
            "binary" => BuildNormalized(baseType, firstArgument, secondArgument),
            "bytea" => "bytea",
            "json" => "json",
            "jsonb" => "jsonb",
            "xml" => "xml",
            _ => baseType,
        };
    }

    private static string BuildNormalized(string baseType, int? firstArgument, int? secondArgument)
    {
        if (firstArgument is null)
        {
            return baseType;
        }

        if (secondArgument is null)
        {
            return $"{baseType}({firstArgument.Value})";
        }

        return $"{baseType}({firstArgument.Value},{secondArgument.Value})";
    }

    private static int? TryParseNullableInt(Match match, string groupName)
    {
        if (!match.Success)
        {
            return null;
        }

        string value = match.Groups[groupName].Value;
        return int.TryParse(value, out int parsed) ? parsed : null;
    }

    private static string CollapseWhitespace(string value)
    {
        return WhitespaceRegex().Replace(value.Trim(), " ");
    }

    [GeneratedRegex(
        "^(?<name>[a-z0-9]+)\\s*(?:\\((?<arg1>\\d+)\\s*(?:,\\s*(?<arg2>\\d+))?\\))?$",
        RegexOptions.CultureInvariant
    )]
    private static partial Regex TypePattern();

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    private sealed record ParsedTypeDescriptor(
        string RawType,
        string BaseType,
        string NormalizedType,
        SchemaCanonicalTypeCategory Category,
        int? FirstArgument,
        int? SecondArgument,
        bool IsMySqlTinyIntOneBooleanAlias
    );
}

using System.Globalization;
using System.Text.RegularExpressions;
using DBWeaver.SqlImport.Contracts;
using DBWeaver.SqlImport.Diagnostics;
using DBWeaver.SqlImport.Ids;
using DBWeaver.SqlImport.IR;
using DBWeaver.SqlImport.IR.Expressions;
using DBWeaver.SqlImport.IR.Metadata;
using DBWeaver.SqlImport.Tracing;
using SqlImportBetweenExpr = DBWeaver.SqlImport.IR.Expressions.BetweenExpr;
using SqlImportCaseExpr = DBWeaver.SqlImport.IR.Expressions.CaseExpr;
using SqlImportCastExpr = DBWeaver.SqlImport.IR.Expressions.CastExpr;
using SqlImportComparisonOperator = DBWeaver.SqlImport.IR.Expressions.ComparisonOperator;
using SqlImportComparisonExpr = DBWeaver.SqlImport.IR.Expressions.ComparisonExpr;
using SqlImportIsNullExpr = DBWeaver.SqlImport.IR.Expressions.IsNullExpr;
using SqlImportLiteralExpr = DBWeaver.SqlImport.IR.Expressions.LiteralExpr;
using SqlImportNotExpr = DBWeaver.SqlImport.IR.Expressions.NotExpr;

namespace DBWeaver.UI.Services.SqlImport.Mapping;

public sealed class SqlImportPredicateIrParser
{
    public SqlExpression ParseScalarExpression(
        string expression,
        string queryId,
        string exprPath,
        SqlImportClause clause,
        IReadOnlyDictionary<string, string> sourceByAlias,
        string fallbackSourceId,
        ICollection<SqlImportDiagnostic> diagnostics
    )
    {
        return ParseScalar(
            expression,
            queryId,
            exprPath,
            sourceByAlias,
            fallbackSourceId,
            diagnostics,
            clause,
            parentExprId: null
        );
    }

    public SqlExpression Parse(
        string expression,
        string queryId,
        string exprPath,
        SqlImportClause clause,
        IReadOnlyDictionary<string, string> sourceByAlias,
        string fallbackSourceId,
        ICollection<SqlImportDiagnostic> diagnostics
    )
    {
        string trimmed = expression.Trim();
        return ParseInternal(
            trimmed,
            queryId,
            exprPath,
            clause,
            sourceByAlias,
            fallbackSourceId,
            diagnostics,
            parentExprId: null
        );
    }

    public int CountExpressions(SqlExpression expression)
    {
        return expression switch
        {
            LogicalExpr logical => 1 + logical.Operands.Sum(CountExpressions),
            SqlImportNotExpr not => 1 + CountExpressions(not.Operand),
            SqlImportComparisonExpr comparison => 1 + CountExpressions(comparison.Left) + CountExpressions(comparison.Right),
            InExpr inExpr => 1 + CountExpressions(inExpr.Value) + inExpr.Values.Sum(CountExpressions),
            SqlImportBetweenExpr between => 1 + CountExpressions(between.Value) + CountExpressions(between.Low) + CountExpressions(between.High),
            LikeExpr like => 1 + CountExpressions(like.Value) + CountExpressions(like.Pattern),
            SqlImportIsNullExpr isNull => 1 + CountExpressions(isNull.Value),
            FunctionExpr function => 1 + function.Arguments.Sum(CountExpressions),
            SqlImportCastExpr cast => 1 + CountExpressions(cast.Value),
            SqlImportCaseExpr caseExpr => 1
                + caseExpr.Branches.Sum(branch => CountExpressions(branch.WhenExpression) + CountExpressions(branch.ThenExpression))
                + (caseExpr.ElseExpression is null ? 0 : CountExpressions(caseExpr.ElseExpression)),
            _ => 1,
        };
    }

    private SqlExpression ParseInternal(
        string expression,
        string queryId,
        string exprPath,
        SqlImportClause clause,
        IReadOnlyDictionary<string, string> sourceByAlias,
        string fallbackSourceId,
        ICollection<SqlImportDiagnostic> diagnostics,
        string? parentExprId
    )
    {
        string exprHash = ComputeHash(expression);
        string exprId = StableSqlImportIdGenerator.BuildExprId(
            queryId,
            exprPath,
            "Predicate",
            exprHash,
            parentExprId
        );

        IReadOnlyList<string> orParts = SplitTopLevelKeyword(expression, "OR");
        if (orParts.Count > 1)
        {
            var operands = new List<SqlExpression>(orParts.Count);
            for (int index = 0; index < orParts.Count; index++)
            {
                operands.Add(ParseInternal(
                    orParts[index],
                    queryId,
                    $"{exprPath}/or/{index}",
                    clause,
                    sourceByAlias,
                    fallbackSourceId,
                    diagnostics,
                    exprId
                ));
            }

            return new LogicalExpr(
                exprId,
                null,
                SqlImportSemanticType.Boolean,
                CombineResolutionStatus(operands),
                CreateTrace(queryId, exprId),
                CreateNodeMetadata(),
                LogicalOperator.Or,
                operands
            );
        }

        IReadOnlyList<string> andParts = SplitTopLevelKeyword(expression, "AND");
        if (andParts.Count > 1)
        {
            var operands = new List<SqlExpression>(andParts.Count);
            for (int index = 0; index < andParts.Count; index++)
            {
                operands.Add(ParseInternal(
                    andParts[index],
                    queryId,
                    $"{exprPath}/and/{index}",
                    clause,
                    sourceByAlias,
                    fallbackSourceId,
                    diagnostics,
                    exprId
                ));
            }

            return new LogicalExpr(
                exprId,
                null,
                SqlImportSemanticType.Boolean,
                CombineResolutionStatus(operands),
                CreateTrace(queryId, exprId),
                CreateNodeMetadata(),
                LogicalOperator.And,
                operands
            );
        }

        if (Regex.IsMatch(expression, @"^NOT\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            string innerExpression = Regex.Replace(
                expression,
                @"^NOT\s+",
                string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            );

            SqlExpression inner = ParseInternal(
                innerExpression,
                queryId,
                $"{exprPath}/not",
                clause,
                sourceByAlias,
                fallbackSourceId,
                diagnostics,
                exprId
            );

            return new SqlImportNotExpr(
                exprId,
                null,
                SqlImportSemanticType.Boolean,
                inner.ResolutionStatus,
                CreateTrace(queryId, exprId),
                CreateNodeMetadata(),
                inner
            );
        }

        Match isNullMatch = Regex.Match(
            expression,
            @"^(?<left>.+?)\s+IS\s+(?<neg>NOT\s+)?NULL$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        if (isNullMatch.Success)
        {
            SqlExpression value = ParseScalar(
                isNullMatch.Groups["left"].Value,
                queryId,
                $"{exprPath}/is-null/value",
                sourceByAlias,
                fallbackSourceId,
                diagnostics,
                clause,
                exprId
            );

            return new SqlImportIsNullExpr(
                exprId,
                null,
                SqlImportSemanticType.Boolean,
                value.ResolutionStatus,
                CreateTrace(queryId, exprId),
                CreateNodeMetadata(),
                value,
                isNullMatch.Groups["neg"].Success
            );
        }

        Match likeMatch = Regex.Match(
            expression,
            @"^(?<left>.+?)\s+(?<neg>NOT\s+)?LIKE\s+(?<right>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        if (likeMatch.Success)
        {
            SqlExpression left = ParseScalar(
                likeMatch.Groups["left"].Value,
                queryId,
                $"{exprPath}/like/left",
                sourceByAlias,
                fallbackSourceId,
                diagnostics,
                clause,
                exprId
            );

            SqlExpression right = ParseScalar(
                likeMatch.Groups["right"].Value,
                queryId,
                $"{exprPath}/like/right",
                sourceByAlias,
                fallbackSourceId,
                diagnostics,
                clause,
                exprId
            );

            return new LikeExpr(
                exprId,
                null,
                SqlImportSemanticType.Boolean,
                CombineResolutionStatus(left, right),
                CreateTrace(queryId, exprId),
                CreateNodeMetadata(),
                left,
                right,
                likeMatch.Groups["neg"].Success
            );
        }

        Match betweenMatch = Regex.Match(
            expression,
            @"^(?<value>.+?)\s+(?<neg>NOT\s+)?BETWEEN\s+(?<low>.+?)\s+AND\s+(?<high>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        if (betweenMatch.Success)
        {
            SqlExpression value = ParseScalar(
                betweenMatch.Groups["value"].Value,
                queryId,
                $"{exprPath}/between/value",
                sourceByAlias,
                fallbackSourceId,
                diagnostics,
                clause,
                exprId
            );

            SqlExpression low = ParseScalar(
                betweenMatch.Groups["low"].Value,
                queryId,
                $"{exprPath}/between/low",
                sourceByAlias,
                fallbackSourceId,
                diagnostics,
                clause,
                exprId
            );

            SqlExpression high = ParseScalar(
                betweenMatch.Groups["high"].Value,
                queryId,
                $"{exprPath}/between/high",
                sourceByAlias,
                fallbackSourceId,
                diagnostics,
                clause,
                exprId
            );

            return new SqlImportBetweenExpr(
                exprId,
                null,
                SqlImportSemanticType.Boolean,
                CombineResolutionStatus(value, low, high),
                CreateTrace(queryId, exprId),
                CreateNodeMetadata(),
                value,
                low,
                high,
                betweenMatch.Groups["neg"].Success
            );
        }

        Match inMatch = Regex.Match(
            expression,
            @"^(?<value>.+?)\s+(?<neg>NOT\s+)?IN\s*\((?<items>.+)\)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant
        );

        if (inMatch.Success)
        {
            SqlExpression value = ParseScalar(
                inMatch.Groups["value"].Value,
                queryId,
                $"{exprPath}/in/value",
                sourceByAlias,
                fallbackSourceId,
                diagnostics,
                clause,
                exprId
            );

            IReadOnlyList<string> items = SplitTopLevelCommaList(inMatch.Groups["items"].Value);
            var values = new List<SqlExpression>(items.Count);

            for (int index = 0; index < items.Count; index++)
            {
                values.Add(ParseScalar(
                    items[index],
                    queryId,
                    $"{exprPath}/in/item/{index}",
                    sourceByAlias,
                    fallbackSourceId,
                    diagnostics,
                    clause,
                    exprId
                ));
            }

            return new InExpr(
                exprId,
                null,
                SqlImportSemanticType.Boolean,
                CombineResolutionStatus([value, ..values]),
                CreateTrace(queryId, exprId),
                CreateNodeMetadata(),
                value,
                values,
                Subquery: null,
                IsNegated: inMatch.Groups["neg"].Success
            );
        }

        Match comparisonMatch = Regex.Match(
            expression,
            @"^(?<left>.+?)\s*(?<op><>|!=|>=|<=|=|>|<)\s*(?<right>.+)$",
            RegexOptions.CultureInvariant
        );

        if (comparisonMatch.Success)
        {
            SqlExpression left = ParseScalar(
                comparisonMatch.Groups["left"].Value,
                queryId,
                $"{exprPath}/cmp/left",
                sourceByAlias,
                fallbackSourceId,
                diagnostics,
                clause,
                exprId
            );

            SqlExpression right = ParseScalar(
                comparisonMatch.Groups["right"].Value,
                queryId,
                $"{exprPath}/cmp/right",
                sourceByAlias,
                fallbackSourceId,
                diagnostics,
                clause,
                exprId
            );

            return new SqlImportComparisonExpr(
                exprId,
                null,
                SqlImportSemanticType.Boolean,
                CombineResolutionStatus(left, right),
                CreateTrace(queryId, exprId),
                CreateNodeMetadata(),
                left,
                MapComparisonOperator(comparisonMatch.Groups["op"].Value),
                right
            );
        }

        SqlExpression fallback = ParseScalar(
            expression,
            queryId,
            $"{exprPath}/fallback",
            sourceByAlias,
            fallbackSourceId,
            diagnostics,
            clause,
            exprId
        );

        if (fallback.ResolutionStatus is SqlResolutionStatus.Ambiguous or SqlResolutionStatus.Unresolved)
            return fallback;

        return new FunctionExpr(
            exprId,
            null,
            SqlImportSemanticType.Boolean,
            SqlResolutionStatus.Partial,
            CreateTrace(queryId, exprId),
            CreateNodeMetadata(),
            Name: "PREDICATE_GENERIC",
            CanonicalName: null,
            Classification: SqlFunctionClassification.GenericPreserved,
            Arguments: [fallback]
        );
    }

    private SqlExpression ParseScalar(
        string expression,
        string queryId,
        string exprPath,
        IReadOnlyDictionary<string, string> sourceByAlias,
        string fallbackSourceId,
        ICollection<SqlImportDiagnostic> diagnostics,
        SqlImportClause clause,
        string? parentExprId
    )
    {
        string trimmed = expression.Trim();
        string exprHash = ComputeHash(trimmed);
        string exprId = StableSqlImportIdGenerator.BuildExprId(
            queryId,
            exprPath,
            "Scalar",
            exprHash,
            parentExprId
        );

        if (trimmed.Equals("NULL", StringComparison.OrdinalIgnoreCase))
        {
            return new SqlImportLiteralExpr(
                exprId,
                null,
                SqlImportSemanticType.Null,
                SqlResolutionStatus.Resolved,
                CreateTrace(queryId, exprId),
                CreateNodeMetadata(),
                trimmed,
                "null",
                true
            );
        }

        if (decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
        {
            return new SqlImportLiteralExpr(
                exprId,
                null,
                SqlImportSemanticType.Decimal,
                SqlResolutionStatus.Resolved,
                CreateTrace(queryId, exprId),
                CreateNodeMetadata(),
                trimmed,
                trimmed,
                false
            );
        }

        if (Regex.IsMatch(trimmed, @"^'([^']|'')*'$", RegexOptions.CultureInvariant))
        {
            return new SqlImportLiteralExpr(
                exprId,
                null,
                SqlImportSemanticType.Text,
                SqlResolutionStatus.Resolved,
                CreateTrace(queryId, exprId),
                CreateNodeMetadata(),
                trimmed,
                trimmed,
                false
            );
        }

        Match columnMatch = Regex.Match(
            trimmed,
            $@"^(?:(?<qualifier>{SqlImportIdentifierNormalizer.IdentifierPattern})\s*\.\s*)?(?<column>{SqlImportIdentifierNormalizer.IdentifierPattern})$",
            RegexOptions.CultureInvariant
        );

        if (columnMatch.Success)
        {
            string? qualifier = columnMatch.Groups["qualifier"].Success
                ? SqlImportIdentifierNormalizer.NormalizeIdentifierToken(columnMatch.Groups["qualifier"].Value)
                : null;

            string column = SqlImportIdentifierNormalizer.NormalizeIdentifierToken(columnMatch.Groups["column"].Value);

            if (!string.IsNullOrWhiteSpace(qualifier))
            {
                if (sourceByAlias.TryGetValue(qualifier, out string? resolvedSourceId))
                {
                    return new ColumnRefExpr(
                        exprId,
                        null,
                        SqlImportSemanticType.Unknown,
                        SqlResolutionStatus.Resolved,
                        CreateTrace(queryId, exprId),
                        CreateNodeMetadata(),
                        qualifier,
                        column,
                        resolvedSourceId
                    );
                }

                diagnostics.Add(CreateAmbiguousOrUnresolvedDiagnostic(
                    "SQLIMP_0202_COLUMN_UNRESOLVED",
                    clause,
                    $"Column '{qualifier}.{column}' could not be resolved in visible source aliases.",
                    queryId,
                    SqlImportDiagnosticCategory.PartialImport,
                    SqlImportDiagnosticSeverity.Warning
                ));

                return new ColumnRefExpr(
                    exprId,
                    null,
                    SqlImportSemanticType.Unknown,
                    SqlResolutionStatus.Unresolved,
                    CreateTrace(queryId, exprId),
                    CreateNodeMetadata(),
                    qualifier,
                    column,
                    null
                );
            }

            if (sourceByAlias.Count == 1)
            {
                return new ColumnRefExpr(
                    exprId,
                    null,
                    SqlImportSemanticType.Unknown,
                    SqlResolutionStatus.Resolved,
                    CreateTrace(queryId, exprId),
                    CreateNodeMetadata(),
                    null,
                    column,
                    fallbackSourceId
                );
            }

            diagnostics.Add(CreateAmbiguousOrUnresolvedDiagnostic(
                "SQLIMP_0201_COLUMN_AMBIGUOUS",
                clause,
                $"Column '{column}' is ambiguous across multiple sources.",
                queryId,
                SqlImportDiagnosticCategory.AmbiguityUnresolved,
                SqlImportDiagnosticSeverity.Error
            ));

            return new ColumnRefExpr(
                exprId,
                null,
                SqlImportSemanticType.Unknown,
                SqlResolutionStatus.Ambiguous,
                CreateTrace(queryId, exprId),
                CreateNodeMetadata(),
                null,
                column,
                null
            );
        }

        Match functionMatch = Regex.Match(
            trimmed,
            @"^(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<args>.*)\)$",
            RegexOptions.Singleline | RegexOptions.CultureInvariant
        );

        if (functionMatch.Success)
        {
            return new FunctionExpr(
                exprId,
                null,
                SqlImportSemanticType.Unknown,
                SqlResolutionStatus.Partial,
                CreateTrace(queryId, exprId),
                CreateNodeMetadata(),
                Name: functionMatch.Groups["name"].Value,
                CanonicalName: null,
                Classification: SqlFunctionClassification.GenericPreserved,
                Arguments: []
            );
        }

        return new SqlImportLiteralExpr(
            exprId,
            null,
            SqlImportSemanticType.Unknown,
            SqlResolutionStatus.Partial,
            CreateTrace(queryId, exprId),
            CreateNodeMetadata(),
            trimmed,
            null,
            false
        );
    }

    private static SqlImportComparisonOperator MapComparisonOperator(string op)
    {
        return op switch
        {
            "=" => SqlImportComparisonOperator.Equals,
            "<>" => SqlImportComparisonOperator.NotEquals,
            "!=" => SqlImportComparisonOperator.NotEquals,
            ">" => SqlImportComparisonOperator.GreaterThan,
            ">=" => SqlImportComparisonOperator.GreaterOrEqual,
            "<" => SqlImportComparisonOperator.LessThan,
            "<=" => SqlImportComparisonOperator.LessOrEqual,
            _ => SqlImportComparisonOperator.Equals,
        };
    }

    private static SqlResolutionStatus CombineResolutionStatus(params SqlExpression[] expressions)
    {
        return CombineResolutionStatus(expressions.AsEnumerable());
    }

    private static SqlResolutionStatus CombineResolutionStatus(IEnumerable<SqlExpression> expressions)
    {
        bool hasAmbiguous = false;
        bool hasUnresolved = false;
        bool hasPartial = false;

        foreach (SqlExpression expression in expressions)
        {
            hasAmbiguous |= expression.ResolutionStatus == SqlResolutionStatus.Ambiguous;
            hasUnresolved |= expression.ResolutionStatus == SqlResolutionStatus.Unresolved;
            hasPartial |= expression.ResolutionStatus == SqlResolutionStatus.Partial;
        }

        if (hasAmbiguous)
            return SqlResolutionStatus.Ambiguous;

        if (hasUnresolved)
            return SqlResolutionStatus.Unresolved;

        if (hasPartial)
            return SqlResolutionStatus.Partial;

        return SqlResolutionStatus.Resolved;
    }

    private static IReadOnlyList<string> SplitTopLevelKeyword(string input, string keyword)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;
        int index = 0;

        while (index < input.Length)
        {
            char character = input[index];
            if (character == '(')
            {
                depth++;
                index++;
                continue;
            }

            if (character == ')')
            {
                depth = Math.Max(0, depth - 1);
                index++;
                continue;
            }

            if (depth == 0
                && IsTokenAt(input, index, keyword)
                && IsTokenBoundary(input, index - 1)
                && IsTokenBoundary(input, index + keyword.Length))
            {
                string slice = input[start..index].Trim();
                if (!string.IsNullOrWhiteSpace(slice))
                    result.Add(slice);

                index += keyword.Length;
                start = index;
                continue;
            }

            index++;
        }

        string tail = input[start..].Trim();
        if (!string.IsNullOrWhiteSpace(tail))
            result.Add(tail);

        return result.Count == 0 ? [input.Trim()] : result;
    }

    private static IReadOnlyList<string> SplitTopLevelCommaList(string input)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;

        for (int index = 0; index < input.Length; index++)
        {
            if (input[index] == '(')
            {
                depth++;
                continue;
            }

            if (input[index] == ')')
            {
                depth = Math.Max(0, depth - 1);
                continue;
            }

            if (input[index] == ',' && depth == 0)
            {
                string item = input[start..index].Trim();
                if (!string.IsNullOrWhiteSpace(item))
                    result.Add(item);

                start = index + 1;
            }
        }

        string tail = input[start..].Trim();
        if (!string.IsNullOrWhiteSpace(tail))
            result.Add(tail);

        return result;
    }

    private static bool IsTokenAt(string input, int start, string token)
    {
        if (start < 0 || start + token.Length > input.Length)
            return false;

        return string.Compare(input, start, token, 0, token.Length, StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static bool IsTokenBoundary(string input, int index)
    {
        if (index < 0 || index >= input.Length)
            return true;

        char character = input[index];
        return !char.IsLetterOrDigit(character) && character != '_';
    }

    private static string ComputeHash(string input)
    {
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    private static TraceMeta CreateTrace(string queryId, string exprId)
    {
        return new TraceMeta(queryId, exprId, queryId, null);
    }

    private static SqlIrNodeMetadata CreateNodeMetadata()
    {
        return new SqlIrNodeMetadata(false, null, [], []);
    }

    private static SqlImportDiagnostic CreateAmbiguousOrUnresolvedDiagnostic(
        string code,
        SqlImportClause clause,
        string message,
        string queryId,
        SqlImportDiagnosticCategory category,
        SqlImportDiagnosticSeverity severity
    )
    {
        return new SqlImportDiagnostic(
            code,
            category,
            severity,
            message,
            clause,
            null,
            null,
            SqlImportDiagnosticAction.ContinuePartial,
            "Qualify the column with explicit source alias to remove ambiguity.",
            queryId,
            queryId
        );
    }
}

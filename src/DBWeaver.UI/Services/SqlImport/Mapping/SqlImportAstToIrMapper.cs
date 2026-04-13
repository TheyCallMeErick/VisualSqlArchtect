using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DBWeaver.Core;
using DBWeaver.SqlImport.Contracts;
using DBWeaver.SqlImport.Diagnostics;
using DBWeaver.SqlImport.Ids;
using DBWeaver.SqlImport.IR;
using DBWeaver.SqlImport.IR.Expressions;
using DBWeaver.SqlImport.IR.Metadata;
using DBWeaver.SqlImport.IR.Sources;
using DBWeaver.SqlImport.Semantics.SymbolTable;
using DBWeaver.SqlImport.Tracing;
using DBWeaver.UI.Services.SqlImport.Execution.Parsing;
using SqlImportLiteralExpr = DBWeaver.SqlImport.IR.Expressions.LiteralExpr;

namespace DBWeaver.UI.Services.SqlImport.Mapping;

public sealed class SqlImportAstToIrMapper
{
    private readonly SqlImportPredicateIrParser _predicateParser = new();

    public SqlToNodeIR MapSelectFrom(
        SqlImportParsedQuery parsed,
        string sql,
        DatabaseProvider provider,
        IReadOnlyCollection<string>? featureFlags = null
    )
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        if (parsed.FromParts.Count == 0)
            throw new InvalidOperationException("AST→IR mapping requires at least one FROM source.");

        var effectiveFeatureFlags = featureFlags?.Count > 0
            ? featureFlags.OrderBy(static value => value, StringComparer.Ordinal).ToList()
            : ["SqlImport.AstIrPrimary"];

        SqlImportDialect dialect = MapDialect(provider);
        string sourceHash = ComputeSourceHash(NormalizeSql(sql));
        string queryId = StableSqlImportIdGenerator.BuildQueryId(
            dialect.ToString().ToLowerInvariant(),
            sourceHash,
            effectiveFeatureFlags
        );
        bool usesTopSyntax = Regex.IsMatch(
            sql,
            @"^\s*SELECT\s+(?:DISTINCT\s+)?TOP\s+\d+\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        string rootScopePath = "root";
        string rootScopeId = StableSqlImportIdGenerator.BuildScopeId(queryId, rootScopePath);

        var diagnostics = new List<SqlImportDiagnostic>();
        var sourceByAlias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        TableRefSourceExpr fromSource = BuildFromSource(parsed.FromParts[0], queryId, rootScopePath, sourceByAlias);

        IReadOnlyList<JoinExpr> joins = BuildJoins(
            parsed,
            queryId,
            rootScopePath,
            sourceByAlias,
            diagnostics
        );

        SqlExpression? whereExpr = null;
        if (!string.IsNullOrWhiteSpace(parsed.WhereClause))
        {
            whereExpr = _predicateParser.Parse(
                parsed.WhereClause,
                queryId,
                "where",
                SqlImportClause.Where,
                sourceByAlias,
                fromSource.SourceId,
                diagnostics
            );
        }

        IReadOnlyList<SelectItemExpr> selectItems = BuildSelectItems(
            parsed,
            queryId,
            fromSource,
            sourceByAlias,
            diagnostics
        );

        var selectAliases = new HashSet<string>(
            selectItems.Select(item => item.AliasMeta.NormalizedAlias),
            StringComparer.OrdinalIgnoreCase
        );

        IReadOnlyList<SqlExpression> groupBy = BuildGroupByExpressions(
            parsed,
            queryId,
            sourceByAlias,
            fromSource.SourceId,
            diagnostics
        );

        IReadOnlyList<OrderByExpr> orderBy = BuildOrderByExpressions(
            parsed,
            queryId,
            sourceByAlias,
            fromSource.SourceId,
            selectAliases,
            diagnostics
        );

        SqlExpression? havingExpr = null;
        if (!string.IsNullOrWhiteSpace(parsed.HavingClause))
        {
            havingExpr = _predicateParser.Parse(
                parsed.HavingClause,
                queryId,
                "having",
                SqlImportClause.Having,
                sourceByAlias,
                fromSource.SourceId,
                diagnostics
            );
        }

        IReadOnlyList<SetOperationExpr> setOperations = BuildSetOperations(
            sql,
            provider,
            effectiveFeatureFlags,
            queryId,
            diagnostics
        );

        QueryExpr query = new(
            selectItems,
            fromSource,
            joins,
            whereExpr,
            groupBy,
            havingExpr,
            orderBy,
            parsed.Limit.HasValue ? new LimitOrTopExpr(parsed.Limit.Value, usesTopSyntax) : null,
            setOperations
        );

        SymbolTableModel symbolTable = BuildSymbolTable(rootScopeId, fromSource, joins, selectItems);

        int whereExpressionCount = whereExpr is null ? 0 : _predicateParser.CountExpressions(whereExpr);
        int joinExpressionCount = joins
            .Where(join => join.OnExpr is not null)
            .Sum(join => _predicateParser.CountExpressions(join.OnExpr!));
        int groupByExpressionCount = groupBy.Sum(expression => _predicateParser.CountExpressions(expression));
        int orderByExpressionCount = orderBy.Sum(order => _predicateParser.CountExpressions(order.Expression));
        int havingExpressionCount = havingExpr is null ? 0 : _predicateParser.CountExpressions(havingExpr);
        int totalExpressionCount = selectItems.Count
            + whereExpressionCount
            + joinExpressionCount
            + groupByExpressionCount
            + orderByExpressionCount
            + havingExpressionCount;
        int unresolvedExpressions = selectItems.Count(item => item.Expression.ResolutionStatus == SqlResolutionStatus.Unresolved)
            + (whereExpr is not null && whereExpr.ResolutionStatus == SqlResolutionStatus.Unresolved ? 1 : 0)
            + joins.Count(join => join.OnExpr is not null && join.OnExpr.ResolutionStatus == SqlResolutionStatus.Unresolved)
            + groupBy.Count(expression => expression.ResolutionStatus == SqlResolutionStatus.Unresolved)
            + orderBy.Count(order => order.ResolutionStatus == SqlResolutionStatus.Unresolved)
            + (havingExpr is not null && havingExpr.ResolutionStatus == SqlResolutionStatus.Unresolved ? 1 : 0);
        int ambiguousExpressions = selectItems.Count(item => item.Expression.ResolutionStatus == SqlResolutionStatus.Ambiguous)
            + (whereExpr is not null && whereExpr.ResolutionStatus == SqlResolutionStatus.Ambiguous ? 1 : 0)
            + joins.Count(join => join.OnExpr is not null && join.OnExpr.ResolutionStatus == SqlResolutionStatus.Ambiguous)
            + groupBy.Count(expression => expression.ResolutionStatus == SqlResolutionStatus.Ambiguous)
            + orderBy.Count(order => order.ResolutionStatus == SqlResolutionStatus.Ambiguous)
            + (havingExpr is not null && havingExpr.ResolutionStatus == SqlResolutionStatus.Ambiguous ? 1 : 0);
        int partialExpressions = selectItems.Count(item => item.Expression.ResolutionStatus == SqlResolutionStatus.Partial)
            + (whereExpr is not null && whereExpr.ResolutionStatus == SqlResolutionStatus.Partial ? 1 : 0)
            + joins.Count(join => join.OnExpr is not null && join.OnExpr.ResolutionStatus == SqlResolutionStatus.Partial)
            + groupBy.Count(expression => expression.ResolutionStatus == SqlResolutionStatus.Partial)
            + orderBy.Count(order => order.ResolutionStatus == SqlResolutionStatus.Partial)
            + (havingExpr is not null && havingExpr.ResolutionStatus == SqlResolutionStatus.Partial ? 1 : 0);

        return new SqlToNodeIR(
            IrVersion: "1.0.0",
            QueryId: queryId,
            SourceHash: sourceHash,
            Dialect: dialect,
            FeatureFlags: effectiveFeatureFlags,
            Query: query,
            SymbolTable: symbolTable,
            Diagnostics: diagnostics,
            Metrics: new IrMetrics(
                TotalSelectItems: selectItems.Count,
                TotalSources: joins.Count + 1,
                TotalJoins: joins.Count,
                TotalExpressions: totalExpressionCount,
                UnresolvedExpressions: unresolvedExpressions,
                AmbiguousExpressions: ambiguousExpressions,
                PartialExpressions: partialExpressions
            ),
            IdGenerationMeta: StableSqlImportIdGenerator.CreateDefaultMeta()
        );
    }

    private IReadOnlyList<JoinExpr> BuildJoins(
        SqlImportParsedQuery parsed,
        string queryId,
        string rootScopePath,
        Dictionary<string, string> sourceByAlias,
        ICollection<SqlImportDiagnostic> diagnostics
    )
    {
        if (parsed.FromParts.Count <= 1)
            return [];

        var joins = new List<JoinExpr>(parsed.FromParts.Count - 1);
        for (int index = 1; index < parsed.FromParts.Count; index++)
        {
            SqlImportSourcePart part = parsed.FromParts[index];
            int joinOrdinal = index - 1;
            string sourceId = StableSqlImportIdGenerator.BuildSourceId(
                queryId,
                rootScopePath,
                sourceOrdinal: index,
                sourceSignature: part.Table
            );

            (string? database, string? schema, string table) = SplitQualifiedSource(part.Table);
            string inferredAlias = part.Alias ?? SqlImportIdentifierNormalizer.NormalizeIdentifierToken(table);

            if (!string.IsNullOrWhiteSpace(inferredAlias))
                sourceByAlias[inferredAlias] = sourceId;

            var rightSource = new TableRefSourceExpr(
                SourceId: sourceId,
                Database: database,
                Schema: schema,
                Table: table,
                Alias: inferredAlias,
                ResolutionStatus: SqlResolutionStatus.Resolved,
                NodeMetadata: CreateNodeMetadata()
            );

            SqlJoinType joinType = ParseJoinType(part.JoinType);
            SqlExpression? onExpr = null;
            if (joinType != SqlJoinType.Cross && !string.IsNullOrWhiteSpace(part.OnClause))
            {
                onExpr = _predicateParser.Parse(
                    part.OnClause,
                    queryId,
                    $"join/{joinOrdinal}/on",
                    SqlImportClause.Join,
                    sourceByAlias,
                    sourceId,
                    diagnostics
                );
            }

            string joinId = StableSqlImportIdGenerator.BuildJoinId(
                queryId,
                joinOrdinal,
                joinType.ToString(),
                part.Table,
                part.OnClause is null ? "no_on_clause" : ComputeSourceHash(part.OnClause)
            );

            SqlResolutionStatus joinResolution = onExpr?.ResolutionStatus ?? SqlResolutionStatus.Resolved;
            joins.Add(new JoinExpr(
                JoinId: joinId,
                JoinType: joinType,
                RightSource: rightSource,
                OnExpr: onExpr,
                Ordinal: joinOrdinal,
                ResolutionStatus: joinResolution,
                NodeMetadata: CreateNodeMetadata()
            ));

            if (joinType != SqlJoinType.Cross && onExpr is null)
            {
                diagnostics.Add(CreateWarning(
                    "SQLIMP_0002_AST_UNSUPPORTED",
                    SqlImportClause.Join,
                    "JOIN without ON clause is currently treated as unsupported in AST→IR P0 mapping.",
                    queryId
                ));
            }
        }

        return joins;
    }

    private IReadOnlyList<SetOperationExpr> BuildSetOperations(
        string sql,
        DatabaseProvider provider,
        IReadOnlyCollection<string> featureFlags,
        string queryId,
        ICollection<SqlImportDiagnostic> diagnostics
    )
    {
        if (!TrySplitFirstSetOperation(sql, out string operatorToken, out bool isAll, out string rightSql))
            return [];

        if (!operatorToken.Equals("UNION", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(CreateWarning(
                "SQLIMP_0002_AST_UNSUPPORTED",
                SqlImportClause.Unknown,
                $"{operatorToken.ToUpperInvariant()} is not mapped in AST→IR P0 and is currently tracked as partial support.",
                queryId
            ));
            return [];
        }

        if (Regex.IsMatch(rightSql, @"\b(UNION|INTERSECT|EXCEPT)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            diagnostics.Add(CreateWarning(
                "SQLIMP_0002_AST_UNSUPPORTED",
                SqlImportClause.Unknown,
                "Nested set operations are not yet mapped in AST→IR P0.",
                queryId
            ));
            return [];
        }

        if (!TryParseSimpleSelectOperand(rightSql, out SqlImportParsedQuery? rightParsed))
        {
            diagnostics.Add(CreateWarning(
                "SQLIMP_0002_AST_UNSUPPORTED",
                SqlImportClause.Unknown,
                "Set operation right operand could not be parsed into AST→IR P0 shape.",
                queryId
            ));
            return [];
        }

        SqlToNodeIR rightIr;
        try
        {
            rightIr = MapSelectFrom(rightParsed!, rightSql, provider, featureFlags);
        }
        catch
        {
            diagnostics.Add(CreateWarning(
                "SQLIMP_0002_AST_UNSUPPORTED",
                SqlImportClause.Unknown,
                "Set operation right operand mapping failed in AST→IR P0.",
                queryId
            ));
            return [];
        }

        return [new SetOperationExpr("UNION", rightIr.Query, isAll)];
    }

    private IReadOnlyList<SqlExpression> BuildGroupByExpressions(
        SqlImportParsedQuery parsed,
        string queryId,
        IReadOnlyDictionary<string, string> sourceByAlias,
        string fallbackSourceId,
        ICollection<SqlImportDiagnostic> diagnostics
    )
    {
        if (string.IsNullOrWhiteSpace(parsed.GroupBy))
            return [];

        IReadOnlyList<string> terms = SplitCommaTerms(parsed.GroupBy);
        var expressions = new List<SqlExpression>(terms.Count);
        for (int index = 0; index < terms.Count; index++)
        {
            expressions.Add(_predicateParser.ParseScalarExpression(
                terms[index],
                queryId,
                $"group-by/{index}",
                SqlImportClause.GroupBy,
                sourceByAlias,
                fallbackSourceId,
                diagnostics
            ));
        }

        return expressions;
    }

    private IReadOnlyList<OrderByExpr> BuildOrderByExpressions(
        SqlImportParsedQuery parsed,
        string queryId,
        IReadOnlyDictionary<string, string> sourceByAlias,
        string fallbackSourceId,
        ISet<string> selectAliases,
        ICollection<SqlImportDiagnostic> diagnostics
    )
    {
        if (string.IsNullOrWhiteSpace(parsed.OrderBy))
            return [];

        IReadOnlyList<string> terms = SplitCommaTerms(parsed.OrderBy);
        var orderBy = new List<OrderByExpr>(terms.Count);

        for (int index = 0; index < terms.Count; index++)
        {
            string term = terms[index].Trim();
            Match directionMatch = Regex.Match(
                term,
                @"^(?<expr>.+?)\s+(?<dir>ASC|DESC)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            );

            bool descending = directionMatch.Success
                && directionMatch.Groups["dir"].Value.Equals("DESC", StringComparison.OrdinalIgnoreCase);

            string orderExpression = directionMatch.Success ? directionMatch.Groups["expr"].Value.Trim() : term;
            string normalizedOrderExpression = NormalizeAlias(orderExpression);

            SqlExpression expression;
            SqlResolutionStatus resolutionStatus;

            if (selectAliases.Contains(normalizedOrderExpression)
                && !Regex.IsMatch(orderExpression, @"\.", RegexOptions.CultureInvariant))
            {
                string exprId = StableSqlImportIdGenerator.BuildExprId(
                    queryId,
                    $"order-by/{index}",
                    nameof(ColumnRefExpr),
                    ComputeSourceHash(orderExpression)
                );

                expression = new ColumnRefExpr(
                    exprId,
                    null,
                    SqlImportSemanticType.Unknown,
                    SqlResolutionStatus.Resolved,
                    CreateTrace(queryId, exprId),
                    CreateNodeMetadata(),
                    null,
                    orderExpression,
                    null
                );
                resolutionStatus = SqlResolutionStatus.Resolved;
            }
            else
            {
                expression = _predicateParser.ParseScalarExpression(
                    orderExpression,
                    queryId,
                    $"order-by/{index}",
                    SqlImportClause.OrderBy,
                    sourceByAlias,
                    fallbackSourceId,
                    diagnostics
                );
                resolutionStatus = expression.ResolutionStatus;

                if (resolutionStatus is SqlResolutionStatus.Unresolved or SqlResolutionStatus.Ambiguous)
                {
                    diagnostics.Add(CreateWarning(
                        "SQLIMP_0202_COLUMN_UNRESOLVED",
                        SqlImportClause.OrderBy,
                        $"ORDER BY expression '{orderExpression}' could not be fully resolved.",
                        queryId
                    ));
                }
            }

            orderBy.Add(new OrderByExpr(
                Expression: expression,
                Descending: descending,
                ResolutionStatus: resolutionStatus,
                NodeMetadata: CreateNodeMetadata()
            ));
        }

        return orderBy;
    }

    private static TableRefSourceExpr BuildFromSource(
        SqlImportSourcePart source,
        string queryId,
        string scopePath,
        Dictionary<string, string> sourceByAlias
    )
    {
        string sourceId = StableSqlImportIdGenerator.BuildSourceId(
            queryId,
            scopePath,
            sourceOrdinal: 0,
            sourceSignature: source.Table
        );

        (string? database, string? schema, string table) = SplitQualifiedSource(source.Table);

        string inferredAlias = source.Alias
            ?? SqlImportIdentifierNormalizer.NormalizeIdentifierToken(table);

        if (!string.IsNullOrWhiteSpace(inferredAlias))
            sourceByAlias[inferredAlias] = sourceId;

        return new TableRefSourceExpr(
            SourceId: sourceId,
            Database: database,
            Schema: schema,
            Table: table,
            Alias: inferredAlias,
            ResolutionStatus: SqlResolutionStatus.Resolved,
            NodeMetadata: CreateNodeMetadata()
        );
    }

    private static IReadOnlyList<SelectItemExpr> BuildSelectItems(
        SqlImportParsedQuery parsed,
        string queryId,
        TableRefSourceExpr fromSource,
        IReadOnlyDictionary<string, string> sourceByAlias,
        ICollection<SqlImportDiagnostic> diagnostics
    )
    {
        if (parsed.IsStar)
        {
            string exprId = StableSqlImportIdGenerator.BuildExprId(
                queryId,
                "select/0",
                nameof(StarExpr),
                "star_expr_hash"
            );

            var star = new StarExpr(
                ExprId: exprId,
                SourceSpan: null,
                SemanticType: SqlImportSemanticType.Unknown,
                ResolutionStatus: SqlResolutionStatus.Partial,
                TraceMeta: CreateTrace(queryId, exprId),
                NodeMetadata: CreateNodeMetadata(),
                Qualifier: parsed.StarQualifier
            );

            var aliasMeta = new AliasMeta(
                OriginalAlias: "*",
                NormalizedAlias: "star",
                DisplayAlias: "*",
                NormalizationRule: "star_preserved",
                NormalizationLossFlags: []
            );

            string selectItemId = StableSqlImportIdGenerator.BuildSelectItemId(
                queryId,
                selectOrdinal: 0,
                exprAstHash: "star_expr_hash",
                normalizedAlias: aliasMeta.NormalizedAlias
            );

            diagnostics.Add(CreateWarning(
                "SQLIMP_0851_STAR_PRESERVED_MISSING_METADATA",
                SqlImportClause.Star,
                "Star projection was preserved in this first AST→IR slice and is classified as partial until expansion rules are enabled.",
                queryId
            ));

            return
            [
                new SelectItemExpr(
                    SelectItemId: selectItemId,
                    Expression: star,
                    AliasMeta: aliasMeta,
                    Ordinal: 0,
                    SemanticType: SqlImportSemanticType.Unknown,
                    SourceSpan: new SourceSpan(1, 1, 1, 1, "star"),
                    NodeMetadata: CreateNodeMetadata()
                ),
            ];
        }

        var selectItems = new List<SelectItemExpr>(parsed.SelectedColumns.Count);

        for (int index = 0; index < parsed.SelectedColumns.Count; index++)
        {
            SqlImportSelectedColumn selected = parsed.SelectedColumns[index];
            SqlExpression expression = BuildSelectExpression(
                selected.Expr,
                queryId,
                index,
                fromSource,
                sourceByAlias
            );

            string aliasSeed = selected.Alias ?? ExtractAliasSeed(selected.Expr, index);
            string normalizedAlias = NormalizeAlias(aliasSeed);

            var aliasMeta = new AliasMeta(
                OriginalAlias: selected.Alias,
                NormalizedAlias: normalizedAlias,
                DisplayAlias: selected.Alias ?? aliasSeed,
                NormalizationRule: "snake_case_ascii",
                NormalizationLossFlags: []
            );

            string selectItemId = StableSqlImportIdGenerator.BuildSelectItemId(
                queryId,
                selectOrdinal: index,
                exprAstHash: ComputeSourceHash(selected.Expr),
                normalizedAlias: normalizedAlias
            );

            selectItems.Add(
                new SelectItemExpr(
                    SelectItemId: selectItemId,
                    Expression: expression,
                    AliasMeta: aliasMeta,
                    Ordinal: index,
                    SemanticType: expression.SemanticType,
                    SourceSpan: new SourceSpan(1, 1, 1, Math.Max(1, selected.Expr.Length), ComputeSourceHash(selected.Expr)),
                    NodeMetadata: CreateNodeMetadata()
                )
            );
        }

        return selectItems;
    }

    private static SqlExpression BuildSelectExpression(
        string expression,
        string queryId,
        int selectIndex,
        TableRefSourceExpr fromSource,
        IReadOnlyDictionary<string, string> sourceByAlias
    )
    {
        string trimmed = expression.Trim();
        string exprHash = ComputeSourceHash(trimmed);
        string exprId = StableSqlImportIdGenerator.BuildExprId(
            queryId,
            $"select/{selectIndex}",
            "SelectExpression",
            exprHash
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
                Raw: trimmed,
                Normalized: "null",
                IsNullLiteral: true
            );
        }

        if (decimal.TryParse(trimmed, out _))
        {
            return new SqlImportLiteralExpr(
                exprId,
                null,
                SqlImportSemanticType.Decimal,
                SqlResolutionStatus.Resolved,
                CreateTrace(queryId, exprId),
                CreateNodeMetadata(),
                Raw: trimmed,
                Normalized: trimmed,
                IsNullLiteral: false
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
                Raw: trimmed,
                Normalized: trimmed,
                IsNullLiteral: false
            );
        }

        Match functionMatch = Regex.Match(
            trimmed,
            @"^(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<args>.*)\)$",
            RegexOptions.Singleline | RegexOptions.CultureInvariant
        );

        if (functionMatch.Success)
        {
            string functionName = functionMatch.Groups["name"].Value;
            return new FunctionExpr(
                exprId,
                null,
                SqlImportSemanticType.Unknown,
                SqlResolutionStatus.Partial,
                CreateTrace(queryId, exprId),
                CreateNodeMetadata(),
                Name: functionName,
                CanonicalName: null,
                Classification: SqlFunctionClassification.GenericPreserved,
                Arguments: []
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

            SqlResolutionStatus resolutionStatus;
            string? boundSourceId;

            if (string.IsNullOrWhiteSpace(qualifier))
            {
                resolutionStatus = SqlResolutionStatus.Resolved;
                boundSourceId = fromSource.SourceId;
            }
            else if (sourceByAlias.TryGetValue(qualifier, out string? resolvedSourceId))
            {
                resolutionStatus = SqlResolutionStatus.Resolved;
                boundSourceId = resolvedSourceId;
            }
            else
            {
                resolutionStatus = SqlResolutionStatus.Unresolved;
                boundSourceId = null;
            }

            return new ColumnRefExpr(
                exprId,
                null,
                SqlImportSemanticType.Unknown,
                resolutionStatus,
                CreateTrace(queryId, exprId),
                CreateNodeMetadata(),
                Qualifier: qualifier,
                Column: column,
                BoundSourceId: boundSourceId
            );
        }

        return new SqlImportLiteralExpr(
            exprId,
            null,
            SqlImportSemanticType.Unknown,
            SqlResolutionStatus.Partial,
            CreateTrace(queryId, exprId),
            CreateNodeMetadata(),
            Raw: trimmed,
            Normalized: null,
            IsNullLiteral: false
        );
    }

    private static SymbolTableModel BuildSymbolTable(
        string rootScopeId,
        TableRefSourceExpr fromSource,
        IReadOnlyList<JoinExpr> joins,
        IReadOnlyList<SelectItemExpr> selectItems
    )
    {
        var sourceSymbols = new Dictionary<string, IReadOnlyList<SourceSymbol>>(StringComparer.OrdinalIgnoreCase);
        RegisterSourceSymbol(sourceSymbols, fromSource);
        foreach (JoinExpr join in joins)
        {
            if (join.RightSource is TableRefSourceExpr tableRef)
                RegisterSourceSymbol(sourceSymbols, tableRef);
        }

        var projectionSymbols = new Dictionary<string, IReadOnlyList<ProjectionSymbol>>(StringComparer.OrdinalIgnoreCase);
        foreach (SelectItemExpr item in selectItems)
        {
            string key = item.AliasMeta.NormalizedAlias;
            if (!projectionSymbols.TryGetValue(key, out IReadOnlyList<ProjectionSymbol>? existing))
                existing = [];

            projectionSymbols[key] = [..existing, new ProjectionSymbol(item.SelectItemId, key, key, item.Ordinal)];
        }

        return new SymbolTableModel(
            [
                new Scope(
                    ScopeId: rootScopeId,
                    ScopeType: ScopeType.Root,
                    ParentScopeId: null,
                    SourceSymbols: sourceSymbols,
                    ProjectionSymbols: projectionSymbols
                ),
            ]
        );
    }

    private static void RegisterSourceSymbol(
        IDictionary<string, IReadOnlyList<SourceSymbol>> sourceSymbols,
        TableRefSourceExpr source
    )
    {
        string sourceSymbol = source.Alias ?? source.Table;
        sourceSymbols[sourceSymbol] =
        [
            new SourceSymbol(
                SourceId: source.SourceId,
                Symbol: sourceSymbol,
                NormalizedKey: sourceSymbol,
                Schema: source.Schema,
                Table: source.Table,
                Alias: source.Alias
            ),
        ];
    }

    private static SqlJoinType ParseJoinType(string? joinType)
    {
        if (string.IsNullOrWhiteSpace(joinType))
            return SqlJoinType.Inner;

        string normalized = joinType.Trim().ToUpperInvariant();
        if (normalized.Contains("LEFT", StringComparison.Ordinal))
            return SqlJoinType.Left;
        if (normalized.Contains("RIGHT", StringComparison.Ordinal))
            return SqlJoinType.Right;
        if (normalized.Contains("FULL", StringComparison.Ordinal))
            return SqlJoinType.Full;
        if (normalized.Contains("CROSS", StringComparison.Ordinal))
            return SqlJoinType.Cross;

        return SqlJoinType.Inner;
    }

    private static SqlImportDialect MapDialect(DatabaseProvider provider)
    {
        return provider switch
        {
            DatabaseProvider.SqlServer => SqlImportDialect.SqlServer,
            DatabaseProvider.MySql => SqlImportDialect.MySql,
            DatabaseProvider.Postgres => SqlImportDialect.Postgres,
            DatabaseProvider.SQLite => SqlImportDialect.SQLite,
            _ => SqlImportDialect.Postgres,
        };
    }

    private static (string? Database, string? Schema, string Table) SplitQualifiedSource(string source)
    {
        string normalized = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(source);
        string[] parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length switch
        {
            >= 3 => (parts[0], parts[1], parts[2]),
            2 => (null, parts[0], parts[1]),
            1 => (null, null, parts[0]),
            _ => (null, null, normalized),
        };
    }

    private static string NormalizeAlias(string alias)
    {
        string trimmed = alias.Trim();
        string withoutDiacritics = RemoveDiacritics(trimmed);
        string snake = Regex.Replace(withoutDiacritics, @"[^A-Za-z0-9]+", "_");
        snake = Regex.Replace(snake, @"_+", "_").Trim('_').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(snake))
            return "col_0";

        return char.IsLetter(snake[0]) ? snake : $"col_{snake}";
    }

    private static string ExtractAliasSeed(string expression, int ordinal)
    {
        Match identifierTail = Regex.Match(
            expression,
            $@"(?<last>{SqlImportIdentifierNormalizer.IdentifierPattern})$",
            RegexOptions.CultureInvariant
        );

        if (identifierTail.Success)
            return SqlImportIdentifierNormalizer.NormalizeIdentifierToken(identifierTail.Groups["last"].Value);

        return $"col_{ordinal}";
    }

    private static IReadOnlyList<string> SplitCommaTerms(string input)
    {
        var terms = new List<string>();
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
                string term = input[start..index].Trim();
                if (!string.IsNullOrWhiteSpace(term))
                    terms.Add(term);

                start = index + 1;
            }
        }

        string tail = input[start..].Trim();
        if (!string.IsNullOrWhiteSpace(tail))
            terms.Add(tail);

        return terms;
    }

    private static bool TrySplitFirstSetOperation(
        string sql,
        out string operatorToken,
        out bool isAll,
        out string rightSql
    )
    {
        operatorToken = string.Empty;
        isAll = false;
        rightSql = string.Empty;

        int depth = 0;
        bool inString = false;

        for (int index = 0; index < sql.Length; index++)
        {
            char current = sql[index];

            if (current == '\'')
            {
                bool escapedQuote = index + 1 < sql.Length && sql[index + 1] == '\'';
                if (escapedQuote)
                {
                    index++;
                    continue;
                }

                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (current == '(')
            {
                depth++;
                continue;
            }

            if (current == ')')
            {
                depth = Math.Max(0, depth - 1);
                continue;
            }

            if (depth != 0)
                continue;

            Match tokenMatch = Regex.Match(
                sql[index..],
                @"^\s*(UNION|INTERSECT|EXCEPT)\b(?:\s+(ALL))?",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            );

            if (!tokenMatch.Success)
                continue;

            operatorToken = tokenMatch.Groups[1].Value;
            isAll = tokenMatch.Groups[2].Success;
            int consumed = tokenMatch.Length;
            rightSql = sql[(index + consumed)..].Trim();
            return !string.IsNullOrWhiteSpace(rightSql);
        }

        return false;
    }

    private static bool TryParseSimpleSelectOperand(string sql, out SqlImportParsedQuery? parsed)
    {
        parsed = null;

        string workingSql = Regex.Replace(sql, @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(workingSql))
            return false;

        int? topLimit = null;
        Match distinctTopPrefix = Regex.Match(
            workingSql,
            @"^\s*SELECT\s+DISTINCT\s+TOP\s+(?<top>\d+)\s+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        if (distinctTopPrefix.Success)
        {
            topLimit = int.Parse(distinctTopPrefix.Groups["top"].Value);
            workingSql = Regex.Replace(
                workingSql,
                @"^\s*SELECT\s+DISTINCT\s+TOP\s+\d+\s+",
                "SELECT DISTINCT ",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            );
        }
        else
        {
            Match topPrefix = Regex.Match(
                workingSql,
                @"^\s*SELECT\s+TOP\s+(?<top>\d+)\s+",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            );
            if (topPrefix.Success)
            {
                topLimit = int.Parse(topPrefix.Groups["top"].Value);
                workingSql = Regex.Replace(
                    workingSql,
                    @"^\s*SELECT\s+TOP\s+\d+\s+",
                    "SELECT ",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
                );
            }
        }

        Match selectMatch = Regex.Match(
            workingSql,
            @"SELECT\s+(DISTINCT\s+)?(.+?)\s+FROM\b",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );

        if (!selectMatch.Success)
            return false;

        bool isDistinct = selectMatch.Groups[1].Success;
        string columnsPart = selectMatch.Groups[2].Value.Trim();
        Match qualifiedStarMatch = Regex.Match(
            columnsPart,
            $@"^\s*(?<source>{SqlImportIdentifierNormalizer.QualifiedIdentifierPattern})\s*\.\s*\*\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        bool isStar = columnsPart == "*" || qualifiedStarMatch.Success;
        string? starQualifier = qualifiedStarMatch.Success
            ? SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(qualifiedStarMatch.Groups["source"].Value)
            : null;

        var selectedColumns = new List<SqlImportSelectedColumn>();
        if (!isStar)
        {
            foreach (string rawColumn in SplitCommaTerms(columnsPart))
            {
                string column = rawColumn.Trim();
                Match asMatch = Regex.Match(
                    column,
                    $@"^(.+?)\s+AS\s+({SqlImportIdentifierNormalizer.IdentifierPattern})$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
                );

                if (asMatch.Success)
                {
                    selectedColumns.Add(new SqlImportSelectedColumn(
                        asMatch.Groups[1].Value.Trim(),
                        SqlImportIdentifierNormalizer.NormalizeIdentifierToken(asMatch.Groups[2].Value)
                    ));
                }
                else
                {
                    selectedColumns.Add(new SqlImportSelectedColumn(column, null));
                }
            }
        }

        Match fromBlock = Regex.Match(
            workingSql,
            @"FROM\s+(.+?)(?=\s+(?:WHERE|ORDER\s+BY|LIMIT|OFFSET|GROUP\s+BY|HAVING)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        if (!fromBlock.Success)
            return false;

        string fromSql = fromBlock.Groups[1].Value.Trim();
        string[] joinKeywords = ["INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN", "JOIN"];
        var fromParts = new List<SqlImportSourcePart>();

        int firstJoinIdx = -1;
        string upperFrom = fromSql.ToUpperInvariant();
        foreach (string joinKeyword in joinKeywords)
        {
            int idx = upperFrom.IndexOf(joinKeyword, StringComparison.Ordinal);
            if (idx >= 0 && (firstJoinIdx < 0 || idx < firstJoinIdx))
                firstJoinIdx = idx;
        }

        string primaryPart = firstJoinIdx >= 0 ? fromSql[..firstJoinIdx].Trim() : fromSql.Trim();
        (string primaryTable, string? primaryAlias) = ExtractTableAndAlias(primaryPart);
        fromParts.Add(new SqlImportSourcePart(primaryTable, primaryAlias, null, null));

        MatchCollection joinMatches = Regex.Matches(
            fromSql,
            $@"(?:INNER\s+JOIN|LEFT\s+(?:OUTER\s+)?JOIN|RIGHT\s+(?:OUTER\s+)?JOIN|FULL\s+(?:OUTER\s+)?JOIN|JOIN)\s+({SqlImportIdentifierNormalizer.QualifiedIdentifierPattern})(?:\s+(?:AS\s+)?({SqlImportIdentifierNormalizer.IdentifierPattern}))?\s+ON\s+(.+?)(?=\s+(?:INNER\s+JOIN|LEFT\s+(?:OUTER\s+)?JOIN|RIGHT\s+(?:OUTER\s+)?JOIN|FULL\s+(?:OUTER\s+)?JOIN|JOIN)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant
        );

        foreach (Match joinMatch in joinMatches)
        {
            string table = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(joinMatch.Groups[1].Value);
            string? alias = joinMatch.Groups[2].Success
                ? SqlImportIdentifierNormalizer.NormalizeIdentifierToken(joinMatch.Groups[2].Value)
                : null;

            string onClause = joinMatch.Groups[3].Value.Trim();
            string joinType = Regex.Match(
                    joinMatch.Value,
                    @"(?:INNER\s+JOIN|LEFT\s+(?:OUTER\s+)?JOIN|RIGHT\s+(?:OUTER\s+)?JOIN|FULL\s+(?:OUTER\s+)?JOIN|JOIN)",
                    RegexOptions.IgnoreCase
                )
                .Value.Trim()
                .ToUpperInvariant();

            fromParts.Add(new SqlImportSourcePart(table, alias, joinType, onClause));
        }

        Match whereMatch = Regex.Match(
            workingSql,
            @"WHERE\s+(.+?)(?=\s+(?:GROUP\s+BY|HAVING|ORDER\s+BY|LIMIT|OFFSET|TOP)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        string? whereClause = whereMatch.Success ? whereMatch.Groups[1].Value.Trim() : null;

        Match orderMatch = Regex.Match(
            workingSql,
            @"ORDER\s+BY\s+(.+?)(?=\s+(?:LIMIT|OFFSET)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        string? orderBy = orderMatch.Success ? orderMatch.Groups[1].Value.Trim() : null;

        Match groupMatch = Regex.Match(
            workingSql,
            @"GROUP\s+BY\s+(.+?)(?=\s+(?:HAVING|ORDER\s+BY|LIMIT|TOP)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        string? groupBy = groupMatch.Success ? groupMatch.Groups[1].Value.Trim() : null;

        Match havingMatch = Regex.Match(
            workingSql,
            @"HAVING\s+(.+?)(?=\s+(?:ORDER\s+BY|LIMIT|TOP)|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        string? havingClause = havingMatch.Success ? havingMatch.Groups[1].Value.Trim() : null;

        int? limit = null;
        Match limitMatch = Regex.Match(workingSql, @"\bLIMIT\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (limitMatch.Success)
        {
            limit = int.Parse(limitMatch.Groups[1].Value);
        }
        else if (topLimit.HasValue)
        {
            limit = topLimit.Value;
        }

        parsed = new SqlImportParsedQuery(
            IsDistinct: isDistinct,
            IsStar: isStar,
            StarQualifier: starQualifier,
            SelectedColumns: selectedColumns,
            FromParts: fromParts,
            WhereClause: whereClause,
            OrderBy: orderBy,
            GroupBy: groupBy,
            HavingClause: havingClause,
            Limit: limit,
            OuterAliases: []
        );

        return true;
    }

    private static (string Table, string? Alias) ExtractTableAndAlias(string part)
    {
        string trimmed = part.Trim();
        Match match = Regex.Match(
            trimmed,
            $@"^({SqlImportIdentifierNormalizer.QualifiedIdentifierPattern})(?:\s+(?:AS\s+)?({SqlImportIdentifierNormalizer.IdentifierPattern}))?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        if (match.Success)
        {
            string table = SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(match.Groups[1].Value);
            string? alias = match.Groups[2].Success
                ? SqlImportIdentifierNormalizer.NormalizeIdentifierToken(match.Groups[2].Value)
                : null;
            return (table, alias);
        }

        string firstToken = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? trimmed;
        return (SqlImportIdentifierNormalizer.NormalizeQualifiedIdentifier(firstToken), null);
    }

    private static TraceMeta CreateTrace(string queryId, string exprId)
    {
        return new TraceMeta(queryId, exprId, queryId, null);
    }

    private static SqlIrNodeMetadata CreateNodeMetadata()
    {
        return new SqlIrNodeMetadata(false, null, [], []);
    }

    private static SqlImportDiagnostic CreateWarning(
        string code,
        SqlImportClause clause,
        string message,
        string queryId
    )
    {
        return new SqlImportDiagnostic(
            Code: code,
            Category: SqlImportDiagnosticCategory.UnsupportedFeature,
            Severity: SqlImportDiagnosticSeverity.Warning,
            Message: message,
            Clause: clause,
            SourceSpan: null,
            SqlFragment: null,
            Action: SqlImportDiagnosticAction.ContinuePartial,
            RecommendedAction: "Proceed with supported SELECT/FROM mapping and complete remaining clauses in the next milestone.",
            QueryId: queryId,
            CorrelationId: queryId
        );
    }

    private static string NormalizeSql(string sql)
    {
        string normalized = Regex.Replace(sql, @"\s+", " ").Trim();
        return normalized;
    }

    private static string ComputeSourceHash(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string RemoveDiacritics(string value)
    {
        string normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (char character in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character)
                == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}

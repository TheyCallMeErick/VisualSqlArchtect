using System.Diagnostics;
using AkkornStudio.Core;
using AkkornStudio.Metadata;
using AkkornStudio.UI.Services.Search;

namespace AkkornStudio.UI.Services.SqlEditor;

public sealed class SqlCompletionProvider : ISqlCompletionEngine
{
    private const int MaxLightweightSuggestions = 160;
    private const int MaxRankedSuggestions = 120;
    private const int MaxTier1TableSuggestions = 120;
    private static readonly SqlTokenizer Tokenizer = new();
    private static readonly SqlStatementExtractor StatementExtractor = new();
    private static readonly SqlContextDetector ContextDetector = new();
    private static readonly SqlSymbolTableBuilder SymbolTableBuilder = new();
    private static readonly SqlCompletionDocumentWindowExtractor DocumentWindowExtractor = new();
    private static readonly TextSearchService TextSearch = new();
    private readonly CompletionRankingEngine _rankingEngine;
    private readonly CompletionUsageStats _usageStats;
    private readonly SqlCompletionMetadataIndexFactory _metadataIndexFactory;
    private readonly object _semanticCacheSync = new();
    private SemanticCompletionContext? _semanticCache;
    private static readonly Comparison<SqlCompletionSuggestion> LightweightComparison = CompareLightweightSuggestions;
    private static readonly Comparison<SqlCompletionSuggestion> LabelOnlyComparison = CompareSuggestionsByLabel;

    public SqlCompletionProvider(
        CompletionRankingEngine? rankingEngine = null,
        CompletionUsageStats? usageStats = null,
        SqlCompletionMetadataIndexFactory? metadataIndexFactory = null)
    {
        _rankingEngine = rankingEngine ?? new CompletionRankingEngine();
        _usageStats = usageStats ?? new CompletionUsageStats();
        _metadataIndexFactory = metadataIndexFactory ?? new SqlCompletionMetadataIndexFactory();
    }

    private sealed record SemanticCompletionContext(
        string SemanticText,
        DatabaseProvider Provider,
        IReadOnlyList<SqlToken> Tokens,
        SqlStatementContext StatementContext,
        SqlCompletionContext CompletionContext,
        SqlSymbolTable SymbolTable);

    private sealed class CompletionTelemetryAccumulator
    {
        public CompletionTelemetryAccumulator(int cancelledRequests)
        {
            CancelledRequests = Math.Max(0, cancelledRequests);
        }

        public long TokenizationMs { get; set; }
        public long StatementExtractionMs { get; set; }
        public long ContextDetectionMs { get; set; }
        public long SymbolTableMs { get; set; }
        public long MetadataLookupMs { get; set; }
        public long FuzzyMs { get; set; }
        public long RequestBuildMs { get; set; }
        public long LightweightBuildMs { get; set; }
        public long RankedBuildMs { get; set; }
        public long RankingMs { get; set; }
        public long TotalMs { get; set; }
        public long TimeToFirstSuggestionMs { get; set; }
        public int CancelledRequests { get; }

        public long ElapsedMs(Stopwatch stopwatch) => Math.Max(0, (long)Math.Round(stopwatch.Elapsed.TotalMilliseconds));

        public long ElapsedAndRestart(Stopwatch stopwatch)
        {
            long elapsed = ElapsedMs(stopwatch);
            stopwatch.Restart();
            return elapsed;
        }

        public SqlCompletionTelemetry ToTelemetry() => new()
        {
            TokenizationMs = TokenizationMs,
            StatementExtractionMs = StatementExtractionMs,
            ContextDetectionMs = ContextDetectionMs,
            SymbolTableMs = SymbolTableMs,
            MetadataLookupMs = MetadataLookupMs,
            FuzzyMs = FuzzyMs,
            RequestBuildMs = RequestBuildMs,
            LightweightBuildMs = LightweightBuildMs,
            RankedBuildMs = RankedBuildMs,
            RankingMs = RankingMs,
            TotalMs = TotalMs,
            TimeToFirstSuggestionMs = TimeToFirstSuggestionMs,
            CancelledRequests = CancelledRequests,
            BudgetMs = 100,
        };
    }

    private static readonly string[] Keywords =
    [
        "SELECT", "FROM", "WHERE", "JOIN", "LEFT", "RIGHT", "INNER", "OUTER",
        "ON", "GROUP", "BY", "ORDER", "HAVING", "LIMIT", "OFFSET", "INSERT",
        "INTO", "VALUES", "UPDATE", "SET", "DELETE", "CREATE", "ALTER", "DROP",
        "AND", "OR", "NOT", "NULL", "AS", "DISTINCT",
    ];

    public SqlCompletionRequest GetSuggestions(
        string fullText,
        int caretOffset,
        DbMetadata? metadata,
        DatabaseProvider? provider = null,
        string? connectionProfileId = null,
        CancellationToken cancellationToken = default)
        => BuildCompletion(
            new SqlCompletionRequestContext(fullText, caretOffset, metadata, provider ?? metadata?.Provider ?? DatabaseProvider.Postgres, connectionProfileId),
            progress: null,
            cancellationToken: cancellationToken).Request;

    public SqlCompletionStageSnapshot BuildCompletion(
        SqlCompletionRequestContext request,
        IProgress<SqlCompletionStageSnapshot>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request.FullText);
        if (request.CaretOffset < 0 || request.CaretOffset > request.FullText.Length)
            throw new ArgumentOutOfRangeException(nameof(request.CaretOffset));

        cancellationToken.ThrowIfCancellationRequested();

        var telemetry = new CompletionTelemetryAccumulator(request.CancelledRequests);
        Stopwatch totalStopwatch = Stopwatch.StartNew();

        SqlCompletionDocumentWindow window = DocumentWindowExtractor.Extract(request.FullText, request.CaretOffset);
        int prefixStart = FindPrefixStart(window.Text, window.Text.Length);
        string semanticText = prefixStart > 0 ? window.Text[..prefixStart] : string.Empty;
        string prefix = window.Text[prefixStart..];

        SemanticCompletionContext semantic = GetOrBuildSemanticContext(semanticText, request.Provider, telemetry, cancellationToken);

        List<SqlCompletionSuggestion> suggestions = BuildTier0Suggestions(request.Provider);
        SqlCompletionRequest tier0Request = BuildRequestTimed(
            suggestions,
            prefix,
            semantic.SymbolTable,
            request.ConnectionProfileId,
            lightweight: true,
            telemetry);
        SqlCompletionStageSnapshot tier0Snapshot = CreateSnapshot(SqlCompletionPipelineStage.Tier0, tier0Request, telemetry, totalStopwatch, false, true);
        progress?.Report(tier0Snapshot);

        if (tier0Snapshot.HasSuggestions && telemetry.TimeToFirstSuggestionMs == 0)
            telemetry.TimeToFirstSuggestionMs = telemetry.ElapsedMs(totalStopwatch);

        if (request.Metadata is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadataStopwatch = Stopwatch.StartNew();
            SqlCompletionMetadataIndex index = _metadataIndexFactory.GetOrCreate(request.Metadata);
            telemetry.MetadataLookupMs += telemetry.ElapsedAndRestart(metadataStopwatch);

            if (ShouldOfferTableSuggestions(semantic.CompletionContext))
            {
                cancellationToken.ThrowIfCancellationRequested();
                suggestions.AddRange(BuildTier1TableSuggestions(index, prefix, request.Provider));
            }

            SqlCompletionRequest tier1Request = BuildRequestTimed(
                suggestions,
                prefix,
                semantic.SymbolTable,
                request.ConnectionProfileId,
                lightweight: true,
                telemetry);
            SqlCompletionStageSnapshot tier1Snapshot = CreateSnapshot(SqlCompletionPipelineStage.Tier1, tier1Request, telemetry, totalStopwatch, false, tier1Request.Suggestions.Count > 0);
            progress?.Report(tier1Snapshot);

            if (tier1Snapshot.HasSuggestions && telemetry.TimeToFirstSuggestionMs == 0)
                telemetry.TimeToFirstSuggestionMs = telemetry.ElapsedMs(totalStopwatch);

            cancellationToken.ThrowIfCancellationRequested();
            if (IsJoinContext(semantic.CompletionContext))
                suggestions.AddRange(BuildTier2JoinSuggestions(index, semantic.SymbolTable));

            string? qualifier = TryGetQualifier(semanticText);
            if (!string.IsNullOrWhiteSpace(qualifier))
            {
                suggestions.AddRange(BuildTier2QualifiedColumns(index, semantic.SymbolTable, qualifier, telemetry, cancellationToken));
            }
            else if (ShouldOfferColumnSuggestions(semantic.CompletionContext, semantic.SymbolTable, prefix))
            {
                suggestions.AddRange(BuildTier2ScopedColumns(index, semantic.SymbolTable, telemetry, cancellationToken));
            }

            if (IsTableContext(semantic.CompletionContext))
                suggestions.AddRange(BuildTier2Ctes(semantic.SymbolTable));

            SqlCompletionRequest tier2Request = BuildRequestTimed(
                suggestions,
                prefix,
                semantic.SymbolTable,
                request.ConnectionProfileId,
                lightweight: true,
                telemetry);
            SqlCompletionStageSnapshot tier2Snapshot = CreateSnapshot(SqlCompletionPipelineStage.Tier2, tier2Request, telemetry, totalStopwatch, false, tier2Request.Suggestions.Count > 0);
            progress?.Report(tier2Snapshot);

            if (tier2Snapshot.HasSuggestions && telemetry.TimeToFirstSuggestionMs == 0)
                telemetry.TimeToFirstSuggestionMs = telemetry.ElapsedMs(totalStopwatch);
        }

        cancellationToken.ThrowIfCancellationRequested();
        SqlCompletionRequest tier3Request = BuildRequestTimed(
            suggestions,
            prefix,
            semantic.SymbolTable,
            request.ConnectionProfileId,
            lightweight: true,
            telemetry);
        SqlCompletionStageSnapshot tier3Snapshot = CreateSnapshot(SqlCompletionPipelineStage.Tier3, tier3Request, telemetry, totalStopwatch, false, tier3Request.Suggestions.Count > 0);
        progress?.Report(tier3Snapshot);

        if (tier3Snapshot.HasSuggestions && telemetry.TimeToFirstSuggestionMs == 0)
            telemetry.TimeToFirstSuggestionMs = telemetry.ElapsedMs(totalStopwatch);

        cancellationToken.ThrowIfCancellationRequested();
        var rankingStopwatch = Stopwatch.StartNew();
        SqlCompletionRequest finalRequest = BuildRequestTimed(
            suggestions,
            prefix,
            semantic.SymbolTable,
            request.ConnectionProfileId,
            lightweight: false,
            telemetry);
        telemetry.RankingMs += telemetry.ElapsedMs(rankingStopwatch);
        SqlCompletionStageSnapshot finalSnapshot = CreateSnapshot(SqlCompletionPipelineStage.Final, finalRequest, telemetry, totalStopwatch, true, finalRequest.Suggestions.Count > 0);
        progress?.Report(finalSnapshot);
        return finalSnapshot;
    }

    public void RecordAcceptedSuggestion(string? suggestionLabel, string? connectionProfileId)
    {
        if (string.IsNullOrWhiteSpace(suggestionLabel))
            return;

        _usageStats.RecordAccepted(suggestionLabel, connectionProfileId);
    }

    private SqlCompletionStageSnapshot CreateSnapshot(
        SqlCompletionPipelineStage stage,
        SqlCompletionRequest request,
        CompletionTelemetryAccumulator telemetry,
        Stopwatch totalStopwatch,
        bool isFinal,
        bool hasSuggestions)
    {
        telemetry.TotalMs = telemetry.ElapsedMs(totalStopwatch);
        return new SqlCompletionStageSnapshot(
            stage,
            request,
            telemetry.ToTelemetry(),
            isFinal);
    }

    private SqlCompletionRequest BuildRequest(
        IReadOnlyList<SqlCompletionSuggestion> suggestions,
        string prefix,
        SqlSymbolTable symbolTable,
        string? connectionProfileId,
        bool lightweight)
    {
        IReadOnlyList<SqlCompletionSuggestion> ordered = lightweight
            ? BuildLightweightRequestSuggestions(suggestions)
            : BuildRankedRequestSuggestions(suggestions, prefix, symbolTable, connectionProfileId);

        return new SqlCompletionRequest
        {
            PrefixLength = prefix.Length,
            Suggestions = ordered,
        };
    }

    private SqlCompletionRequest BuildRequestTimed(
        IReadOnlyList<SqlCompletionSuggestion> suggestions,
        string prefix,
        SqlSymbolTable symbolTable,
        string? connectionProfileId,
        bool lightweight,
        CompletionTelemetryAccumulator telemetry)
    {
        var stopwatch = Stopwatch.StartNew();
        SqlCompletionRequest request = BuildRequest(
            suggestions,
            prefix,
            symbolTable,
            connectionProfileId,
            lightweight);

        long elapsed = telemetry.ElapsedMs(stopwatch);
        telemetry.RequestBuildMs += elapsed;
        if (lightweight)
            telemetry.LightweightBuildMs += elapsed;
        else
            telemetry.RankedBuildMs += elapsed;

        return request;
    }

    private IReadOnlyList<SqlCompletionSuggestion> BuildLightweightRequestSuggestions(
        IReadOnlyList<SqlCompletionSuggestion> suggestions)
    {
        var deduped = new List<SqlCompletionSuggestion>(Math.Min(suggestions.Count, MaxLightweightSuggestions));
        var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (SqlCompletionSuggestion suggestion in suggestions)
        {
            if (!HasContent(suggestion))
                continue;

            string labelKey = suggestion.Label ?? string.Empty;
            if (!seenLabels.Add(labelKey))
                continue;

            InsertTopSorted(deduped, suggestion, MaxLightweightSuggestions, LightweightComparison);
        }

        return deduped;
    }

    private IReadOnlyList<SqlCompletionSuggestion> BuildRankedRequestSuggestions(
        IReadOnlyList<SqlCompletionSuggestion> suggestions,
        string prefix,
        SqlSymbolTable symbolTable,
        string? connectionProfileId)
    {
        IEnumerable<SqlCompletionSuggestion> ranked = _rankingEngine.Rank(
            suggestions,
            prefix,
            symbolTable,
            _usageStats,
            connectionProfileId);

        var deduped = new List<SqlCompletionSuggestion>(MaxRankedSuggestions);
        var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (SqlCompletionSuggestion suggestion in ranked)
        {
            if (!HasContent(suggestion))
                continue;

            string labelKey = suggestion.Label ?? string.Empty;
            if (!seenLabels.Add(labelKey))
                continue;

            deduped.Add(suggestion);
            if (deduped.Count >= MaxRankedSuggestions)
                break;
        }

        return deduped;
    }

    private static bool HasContent(SqlCompletionSuggestion suggestion)
        => !string.IsNullOrWhiteSpace(suggestion.Label) || !string.IsNullOrWhiteSpace(suggestion.InsertText);

    private static int CompareLightweightSuggestions(
        SqlCompletionSuggestion left,
        SqlCompletionSuggestion right)
    {
        int kindComparison = left.Kind.CompareTo(right.Kind);
        if (kindComparison != 0)
            return kindComparison;

        return StringComparer.OrdinalIgnoreCase.Compare(left.Label, right.Label);
    }

    private static int CompareSuggestionsByLabel(
        SqlCompletionSuggestion left,
        SqlCompletionSuggestion right)
        => StringComparer.OrdinalIgnoreCase.Compare(left.Label, right.Label);

    private static void InsertTopSorted(
        List<SqlCompletionSuggestion> target,
        SqlCompletionSuggestion candidate,
        int maxCount,
        Comparison<SqlCompletionSuggestion> comparison)
    {
        if (maxCount <= 0)
            return;

        int insertIndex = FindInsertIndex(target, candidate, comparison);
        if (target.Count >= maxCount && insertIndex >= maxCount)
            return;

        target.Insert(insertIndex, candidate);

        if (target.Count > maxCount)
            target.RemoveAt(maxCount);
    }

    private static int FindInsertIndex(
        List<SqlCompletionSuggestion> target,
        SqlCompletionSuggestion candidate,
        Comparison<SqlCompletionSuggestion> comparison)
    {
        int low = 0;
        int high = target.Count;

        while (low < high)
        {
            int mid = low + ((high - low) / 2);
            int cmp = comparison(target[mid], candidate);
            if (cmp <= 0)
                low = mid + 1;
            else
                high = mid;
        }

        return low;
    }

    private SemanticCompletionContext GetOrBuildSemanticContext(
        string semanticText,
        DatabaseProvider provider,
        CompletionTelemetryAccumulator telemetry,
        CancellationToken cancellationToken)
    {
        SemanticCompletionContext? cached = _semanticCache;
        if (cached is not null
            && cached.Provider == provider
            && string.Equals(cached.SemanticText, semanticText, StringComparison.Ordinal))
        {
            return cached;
        }

        lock (_semanticCacheSync)
        {
            cached = _semanticCache;
            if (cached is not null
                && cached.Provider == provider
                && string.Equals(cached.SemanticText, semanticText, StringComparison.Ordinal))
            {
                return cached;
            }
        }

        var tokenStopwatch = Stopwatch.StartNew();
        IReadOnlyList<SqlToken> tokens = semanticText.Length == 0 ? [] : Tokenizer.Tokenize(semanticText);
        telemetry.TokenizationMs += telemetry.ElapsedAndRestart(tokenStopwatch);

        cancellationToken.ThrowIfCancellationRequested();
        var statementStopwatch = Stopwatch.StartNew();
        SqlStatementContext statementContext = StatementExtractor.Extract(tokens, semanticText.Length);
        telemetry.StatementExtractionMs += telemetry.ElapsedAndRestart(statementStopwatch);

        var contextStopwatch = Stopwatch.StartNew();
        SqlCompletionContext completionContext = ContextDetector.Detect(statementContext.Tokens, semanticText.Length);
        telemetry.ContextDetectionMs += telemetry.ElapsedAndRestart(contextStopwatch);

        var symbolStopwatch = Stopwatch.StartNew();
        SqlSymbolTable symbolTable = SymbolTableBuilder.Build(semanticText, provider);
        telemetry.SymbolTableMs += telemetry.ElapsedAndRestart(symbolStopwatch);

        SemanticCompletionContext rebuilt = new(
            semanticText,
            provider,
            tokens,
            statementContext,
            completionContext,
            symbolTable);

        lock (_semanticCacheSync)
        {
            cached = _semanticCache;
            if (cached is not null
                && cached.Provider == provider
                && string.Equals(cached.SemanticText, semanticText, StringComparison.Ordinal))
            {
                return cached;
            }

            _semanticCache = rebuilt;
            return rebuilt;
        }
    }

    private List<SqlCompletionSuggestion> BuildTier0Suggestions(DatabaseProvider provider)
    {
        var suggestions = new List<SqlCompletionSuggestion>();
        suggestions.AddRange(SuggestKeywords());
        suggestions.AddRange(SuggestFunctions(provider));
        suggestions.AddRange(SuggestSnippets());
        return suggestions;
    }

    private IReadOnlyList<SqlCompletionSuggestion> BuildTier1TableSuggestions(
        SqlCompletionMetadataIndex index,
        string prefix,
        DatabaseProvider provider)
    {
        var topSuggestions = new List<SqlCompletionSuggestion>(MaxTier1TableSuggestions);
        bool hasPrefix = !string.IsNullOrWhiteSpace(prefix);

        foreach (SqlCompletionSuggestion suggestion in index.TableSuggestions)
        {
            if (hasPrefix && !TextSearch.Matches(prefix, suggestion.Label, suggestion.Detail, suggestion.InsertText))
                continue;

            InsertTopSorted(topSuggestions, suggestion, MaxTier1TableSuggestions, LabelOnlyComparison);
        }

        return topSuggestions;
    }

    private static IReadOnlyList<SqlCompletionSuggestion> BuildTier2Ctes(SqlSymbolTable symbolTable)
        => SuggestCtes(symbolTable).ToList();

    private IReadOnlyList<SqlCompletionSuggestion> BuildTier2JoinSuggestions(
        SqlCompletionMetadataIndex index,
        SqlSymbolTable symbolTable)
        => SuggestSmartJoins(index, symbolTable).ToList();

    private IReadOnlyList<SqlCompletionSuggestion> BuildTier2QualifiedColumns(
        SqlCompletionMetadataIndex index,
        SqlSymbolTable symbolTable,
        string qualifier,
        CompletionTelemetryAccumulator telemetry,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        List<SqlCompletionSuggestion> result = SuggestColumnsForQualifier(index, symbolTable, qualifier).ToList();
        telemetry.FuzzyMs += telemetry.ElapsedAndRestart(stopwatch);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private IReadOnlyList<SqlCompletionSuggestion> BuildTier2ScopedColumns(
        SqlCompletionMetadataIndex index,
        SqlSymbolTable symbolTable,
        CompletionTelemetryAccumulator telemetry,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        List<SqlCompletionSuggestion> result = SuggestColumnsInScope(index, symbolTable).Take(180).ToList();
        telemetry.FuzzyMs += telemetry.ElapsedAndRestart(stopwatch);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private static IEnumerable<SqlCompletionSuggestion> SuggestKeywords() =>
        Keywords.Select(k => new SqlCompletionSuggestion(k, k, "SQL keyword", SqlCompletionKind.Keyword));

    private static IReadOnlyList<SqlCompletionSuggestion> SuggestFunctions(DatabaseProvider provider) =>
        provider switch
        {
            DatabaseProvider.Postgres =>
            [
                CreateFunction("NOW()", "Current timestamp"),
                CreateFunction("DATE_TRUNC('day', )", "Date truncate"),
                CreateFunction("COALESCE(, )", "First non-null value"),
                CreateFunction("STRING_AGG(, ',')", "String aggregation"),
                CreateFunction("JSONB_EXTRACT_PATH_TEXT(, )", "JSONB path extraction"),
            ],
            DatabaseProvider.MySql =>
            [
                CreateFunction("NOW()", "Current timestamp"),
                CreateFunction("DATE_FORMAT(, '%Y-%m-%d')", "Date formatting"),
                CreateFunction("IFNULL(, )", "Null fallback"),
                CreateFunction("GROUP_CONCAT( SEPARATOR ',')", "String aggregation"),
                CreateFunction("JSON_EXTRACT(, '$.path')", "JSON path extraction"),
            ],
            DatabaseProvider.SqlServer =>
            [
                CreateFunction("GETDATE()", "Current timestamp"),
                CreateFunction("DATEADD(day, 1, )", "Date arithmetic"),
                CreateFunction("ISNULL(, )", "Null fallback"),
                CreateFunction("STRING_AGG(, ',')", "String aggregation"),
                CreateFunction("JSON_VALUE(, '$.path')", "JSON path extraction"),
            ],
            DatabaseProvider.SQLite =>
            [
                CreateFunction("datetime('now')", "Current timestamp"),
                CreateFunction("strftime('%Y-%m-%d', )", "Date formatting"),
                CreateFunction("ifnull(, )", "Null fallback"),
                CreateFunction("group_concat(, ',')", "String aggregation"),
                CreateFunction("json_extract(, '$.path')", "JSON path extraction"),
            ],
            _ => [],
        };

    private static SqlCompletionSuggestion CreateFunction(string label, string detail) =>
        new(label, label, detail, SqlCompletionKind.Function);

    private static IReadOnlyList<SqlCompletionSuggestion> SuggestSnippets() =>
    [
        new(
            "SELECT ... FROM ...",
            "SELECT\n  $1\nFROM $2$0",
            "Basic query skeleton",
            SqlCompletionKind.Snippet),
        new(
            "SELECT ... FROM ... WHERE ...",
            "SELECT\n  $1\nFROM $2\nWHERE $3$0",
            "Query skeleton with filter",
            SqlCompletionKind.Snippet),
        new(
            "INSERT INTO ... VALUES ...",
            "INSERT INTO $1\n(\n  $2\n)\nVALUES\n(\n  $3\n);$0",
            "Insert statement skeleton",
            SqlCompletionKind.Snippet),
        new(
            "UPDATE ... SET ... WHERE ...",
            "UPDATE $1\nSET $2\nWHERE $3;$0",
            "Update statement skeleton",
            SqlCompletionKind.Snippet),
    ];

    private static IEnumerable<SqlCompletionSuggestion> SuggestCtes(SqlSymbolTable symbolTable)
    {
        foreach (string cteName in symbolTable.CteNames.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase))
        {
            string alias = BuildAlias(cteName.Split('.').Last());
            yield return new SqlCompletionSuggestion(
                cteName,
                cteName,
                "CTE",
                SqlCompletionKind.Table);
            yield return new SqlCompletionSuggestion(
                $"{cteName} AS {alias}",
                $"{cteName} AS {alias}",
                "CTE with alias",
                SqlCompletionKind.Table);
        }
    }

    private static IEnumerable<SqlCompletionSuggestion> SuggestColumnsForQualifier(
        SqlCompletionMetadataIndex index,
        SqlSymbolTable symbolTable,
        string qualifier)
    {
        string tableRef = qualifier;
        if (symbolTable.TryResolveBinding(qualifier, out SqlTableBindingSymbol? binding)
            && binding is not null
            && !binding.IsSubquery)
        {
            tableRef = binding.TableRef;
        }

        TableMetadata? table = ResolveTable(index, tableRef);
        if (table is null)
            yield break;

        foreach (ColumnMetadata col in table.Columns.OrderBy(c => c.OrdinalPosition))
        {
            yield return new SqlCompletionSuggestion(
                col.Name,
                col.Name,
                BuildColumnDetail(table, col),
                SqlCompletionKind.Column);
        }
    }

    private static IEnumerable<SqlCompletionSuggestion> SuggestColumnsInScope(SqlCompletionMetadataIndex index, SqlSymbolTable symbolTable)
    {
        if (symbolTable.BindingsInOrder.Count == 0)
            yield break;

        foreach (SqlTableBindingSymbol binding in symbolTable.BindingsInOrder)
        {
            if (binding.IsSubquery)
                continue;

            TableMetadata? table = ResolveTable(index, binding.TableRef);
            if (table is null)
                continue;

            foreach (ColumnMetadata col in table.Columns.OrderBy(c => c.OrdinalPosition))
            {
                string label = $"{binding.Alias}.{col.Name}";
                yield return new SqlCompletionSuggestion(
                    label,
                    label,
                    BuildColumnDetail(table, col),
                    SqlCompletionKind.Column);
            }
        }
    }

    private static IEnumerable<SqlCompletionSuggestion> SuggestSmartJoins(SqlCompletionMetadataIndex index, SqlSymbolTable symbolTable)
    {
        SqlTableBindingSymbol? anchor = symbolTable.BindingsInOrder.LastOrDefault(static binding => !binding.IsSubquery);
        if (anchor is null)
            yield break;

        TableMetadata? anchorTable = ResolveTable(index, anchor.TableRef);
        if (anchorTable is null)
            yield break;

        var map = new Dictionary<string, SqlCompletionSuggestion>(StringComparer.OrdinalIgnoreCase);
        foreach (ForeignKeyRelation fk in GetAnchorForeignKeys(index, anchorTable.FullName))
        {
            if (fk.ChildFullTable.Equals(anchorTable.FullName, StringComparison.OrdinalIgnoreCase))
            {
                string targetTable = fk.ParentFullTable;
                string targetAlias = BuildAlias(targetTable.Split('.').Last());
                string insert = $"{targetTable} {targetAlias} ON {targetAlias}.{fk.ParentColumn} = {anchor.Alias}.{fk.ChildColumn}";
                string label = $"JOIN {insert}";
                map[label] = new SqlCompletionSuggestion(label, insert, "Smart join via foreign key", SqlCompletionKind.Join);
            }
            else if (fk.ParentFullTable.Equals(anchorTable.FullName, StringComparison.OrdinalIgnoreCase))
            {
                string targetTable = fk.ChildFullTable;
                string targetAlias = BuildAlias(targetTable.Split('.').Last());
                string insert = $"{targetTable} {targetAlias} ON {targetAlias}.{fk.ChildColumn} = {anchor.Alias}.{fk.ParentColumn}";
                string label = $"JOIN {insert}";
                map[label] = new SqlCompletionSuggestion(label, insert, "Smart join via foreign key", SqlCompletionKind.Join);
            }
        }

        foreach (SqlCompletionSuggestion suggestion in map.Values)
            yield return suggestion;
    }

    private static string BuildColumnDetail(TableMetadata table, ColumnMetadata column)
    {
        var flags = new List<string>();
        if (column.IsPrimaryKey)
            flags.Add("PK");
        if (column.IsForeignKey)
            flags.Add("FK");
        if (!column.IsNullable)
            flags.Add("NOT NULL");

        string suffix = flags.Count == 0 ? string.Empty : $" [{string.Join(", ", flags)}]";
        return $"{table.FullName}.{column.Name} ({column.DataType}){suffix}";
    }

    private static string BuildAlias(string tableName)
    {
        string[] parts = tableName
            .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return "t";
        if (parts.Length == 1)
            return parts[0][0].ToString().ToLowerInvariant();

        return string.Concat(parts.Select(p => char.ToLowerInvariant(p[0])));
    }

    private static TableMetadata? ResolveTable(SqlCompletionMetadataIndex index, string tableRef)
    {
        string normalized = tableRef.Trim();
        if (index.TablesByFullName.TryGetValue(normalized, out TableMetadata? byFullName))
            return byFullName;

        return index.TablesByName.TryGetValue(normalized, out TableMetadata? byName)
            ? byName
            : null;
    }

    private static bool IsTableContext(SqlCompletionContext context)
    {
        return context is SqlCompletionContext.FromClause
            or SqlCompletionContext.JoinClause
            or SqlCompletionContext.InsertColumns;
    }

    private static bool IsJoinContext(SqlCompletionContext context)
    {
        return context == SqlCompletionContext.JoinClause;
    }

    private static bool IsColumnContext(SqlCompletionContext context)
    {
        return context is SqlCompletionContext.SelectList
            or SqlCompletionContext.WhereClause
            or SqlCompletionContext.OnClause
            or SqlCompletionContext.OrderByClause
            or SqlCompletionContext.GroupByClause
            or SqlCompletionContext.HavingClause
            or SqlCompletionContext.UpdateSetClause;
    }

    private static bool ShouldOfferTableSuggestions(SqlCompletionContext context)
    {
        return IsTableContext(context)
            || context is SqlCompletionContext.SelectList or SqlCompletionContext.Unknown;
    }

    private static bool ShouldOfferColumnSuggestions(
        SqlCompletionContext context,
        SqlSymbolTable symbolTable,
        string prefix)
    {
        if (IsColumnContext(context))
            return true;

        if (context == SqlCompletionContext.Unknown && symbolTable.BindingsInOrder.Count > 0)
            return true;

        return context == SqlCompletionContext.SelectList
               && symbolTable.BindingsInOrder.Count > 0
               && !string.IsNullOrWhiteSpace(prefix);
    }

    private static string? TryGetQualifier(string beforeCaret)
    {
        if (string.IsNullOrWhiteSpace(beforeCaret) || beforeCaret[^1] != '.')
            return null;

        int dotIndex = beforeCaret.Length - 1;
        int start = dotIndex - 1;
        while (start >= 0 && (char.IsLetterOrDigit(beforeCaret[start]) || beforeCaret[start] == '_'))
            start--;

        int tokenStart = start + 1;
        if (tokenStart >= dotIndex)
            return null;

        return beforeCaret[tokenStart..dotIndex];
    }

    private static int FindPrefixStart(string fullText, int caretOffset)
    {
        int start = caretOffset;
        while (start > 0 && (char.IsLetterOrDigit(fullText[start - 1]) || fullText[start - 1] == '_'))
            start--;

        return start;
    }

    private static IEnumerable<ForeignKeyRelation> GetAnchorForeignKeys(SqlCompletionMetadataIndex index, string anchorTableFullName)
    {
        if (index.ForeignKeysByTable.TryGetValue(anchorTableFullName, out IReadOnlyList<ForeignKeyRelation>? relations))
        {
            foreach (ForeignKeyRelation relation in relations)
                yield return relation;
        }
    }

}

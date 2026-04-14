using DBWeaver.Core;
using DBWeaver.Metadata;
using DBWeaver.UI.Services.SqlEditor;
using System.Diagnostics;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlCompletionProviderTests
{
    [Fact]
    public void GetSuggestions_WithKeywordPrefix_ReturnsKeywordCompletion()
    {
        var sut = new SqlCompletionProvider();

        SqlCompletionRequest request = sut.GetSuggestions("SEL", 3, metadata: null, DatabaseProvider.Postgres);

        Assert.Equal(3, request.PrefixLength);
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Keyword && s.Label == "SELECT");
    }

    [Fact]
    public void GetSuggestions_InTableContext_ReturnsTableSuggestionsWithAliasVariant()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT * FROM ord";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Table && s.Label == "public.orders");
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Table && s.Label.StartsWith("public.orders AS ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetSuggestions_WithAliasQualifier_ReturnsColumnsFromResolvedTable()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT * FROM public.orders o WHERE o.";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Column && s.Label == "id");
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Column && s.Label == "status");
    }

    [Fact]
    public void GetSuggestions_ForPostgres_IncludesProviderSpecificFunctions()
    {
        var sut = new SqlCompletionProvider();

        SqlCompletionRequest request = sut.GetSuggestions(string.Empty, 0, metadata: null, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Function && s.Label == "NOW()");
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Function && s.Label.StartsWith("DATE_TRUNC", StringComparison.Ordinal));
        Assert.DoesNotContain(request.Suggestions, s => s.Kind == SqlCompletionKind.Function && s.Label == "GETDATE()");
    }

    [Fact]
    public void GetSuggestions_ForSqlServer_IncludesProviderSpecificFunctions()
    {
        var sut = new SqlCompletionProvider();

        SqlCompletionRequest request = sut.GetSuggestions("GE", 2, metadata: null, DatabaseProvider.SqlServer);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Function && s.Label == "GETDATE()");
        Assert.DoesNotContain(request.Suggestions, s => s.Kind == SqlCompletionKind.Function && s.Label == "NOW()");
    }

    [Fact]
    public void GetSuggestions_ForMySql_IncludesProviderSpecificFunctions()
    {
        var sut = new SqlCompletionProvider();

        SqlCompletionRequest request = sut.GetSuggestions("IF", 2, metadata: null, DatabaseProvider.MySql);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Function && s.Label == "IFNULL(, )");
        Assert.DoesNotContain(request.Suggestions, s => s.Kind == SqlCompletionKind.Function && s.Label == "GETDATE()");
    }

    [Fact]
    public void GetSuggestions_ForSqlite_IncludesProviderSpecificFunctions()
    {
        var sut = new SqlCompletionProvider();

        SqlCompletionRequest request = sut.GetSuggestions("dat", 3, metadata: null, DatabaseProvider.SQLite);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Function && s.Label == "datetime('now')");
        Assert.DoesNotContain(request.Suggestions, s => s.Kind == SqlCompletionKind.Function && s.Label == "GETDATE()");
    }

    [Fact]
    public void GetSuggestions_WithoutMetadata_DoesNotReturnJoinSuggestions()
    {
        var sut = new SqlCompletionProvider();
        const string sql = "SELECT * FROM public.orders o JOIN ";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata: null, DatabaseProvider.Postgres);

        Assert.DoesNotContain(request.Suggestions, s => s.Kind == SqlCompletionKind.Join);
    }

    [Fact]
    public void GetSuggestions_InJoinContext_ReturnsSmartJoinSuggestionFromForeignKey()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT * FROM public.orders o JOIN ";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        SqlCompletionSuggestion join = Assert.Single(request.Suggestions, s => s.Kind == SqlCompletionKind.Join);
        Assert.Contains("public.customers", join.InsertText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ON", join.InsertText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("o.customer_id", join.InsertText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSuggestions_IncludesSnippets()
    {
        var sut = new SqlCompletionProvider();

        SqlCompletionRequest request = sut.GetSuggestions(string.Empty, 0, metadata: null, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Snippet && s.Label == "SELECT ... FROM ...");
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Snippet && s.Label == "UPDATE ... SET ... WHERE ...");
    }

    [Fact]
    public void GetSuggestions_SnippetsExposeTabStopMarkers()
    {
        var sut = new SqlCompletionProvider();

        SqlCompletionRequest request = sut.GetSuggestions(string.Empty, 0, metadata: null, DatabaseProvider.Postgres);
        SqlCompletionSuggestion snippet = Assert.Single(
            request.Suggestions,
            static s => s.Kind == SqlCompletionKind.Snippet && s.Label == "SELECT ... FROM ...");

        Assert.Contains("$1", snippet.InsertText, StringComparison.Ordinal);
        Assert.Contains("$2", snippet.InsertText, StringComparison.Ordinal);
        Assert.Contains("$0", snippet.InsertText, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSuggestions_InSecondStatement_UsesCurrentStatementContext()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT 1; SELECT * FROM public.orders o WHERE o.";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Column && s.Label == "id");
    }

    [Fact]
    public void GetSuggestions_IgnoresCommentKeywordsForContextDetection()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "/* FROM users */ SELECT * FROM public.orders o WHERE o.";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Column && s.Label == "id");
        Assert.DoesNotContain(request.Suggestions, s => s.Kind == SqlCompletionKind.Table && s.Label == "public.orders");
    }

    [Fact]
    public void GetSuggestions_InOrderByContext_ReturnsColumnsInScope()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT * FROM public.orders o ORDER BY o.";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Column && s.Label == "id");
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Column && s.Label == "status");
    }

    [Fact]
    public void GetSuggestions_InInsertColumnsContext_ReturnsTableSuggestions()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "INSERT INTO pub";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Table && s.Label == "public.orders");
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Table && s.Label == "public.customers");
    }

    [Fact]
    public void GetSuggestions_InTableContext_IncludesCteSuggestions()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "WITH recent_orders AS (SELECT * FROM public.orders) SELECT * FROM ";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Table && s.Label == "recent_orders");
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Table && s.Label == "recent_orders AS ro");
    }

    [Fact]
    public void GetSuggestions_WithCteAliasQualifier_DoesNotThrowAndKeepsBaselineSuggestions()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "WITH recent_orders AS (SELECT * FROM public.orders) SELECT * FROM recent_orders r WHERE r.";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.NotEmpty(request.Suggestions);
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Keyword && s.Label == "SELECT");
    }

    [Fact]
    public void GetSuggestions_WithUnknownQualifier_DoesNotThrowAndReturnsBaselineSuggestions()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT * FROM public.orders o WHERE x.";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.NotEmpty(request.Suggestions);
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Keyword && s.Label == "SELECT");
    }

    [Fact]
    public void GetSuggestions_WithLargeInput_DoesNotThrowAndReturnsSuggestions()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        string largeSql = "SELECT * FROM public.orders o WHERE o.id = 1 " + new string(' ', 10_500) + "o.";

        SqlCompletionRequest request = sut.GetSuggestions(largeSql, largeSql.Length, metadata, DatabaseProvider.Postgres);

        Assert.NotEmpty(request.Suggestions);
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Column && s.Label == "id");
    }

    [Fact]
    public void GetSuggestions_FuzzyPrefix_FindsCreatedAt()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT * FROM public.orders o WHERE o.crat";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Column && s.Label == "created_at");
    }

    [Fact]
    public void GetSuggestions_FuzzyPrefix_FindsUsersTable()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT * FROM usrs";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Table && s.Label == "public.users");
    }

    [Fact]
    public void GetSuggestions_WithIdPrefix_ExactColumnComesBeforeFuzzyColumns()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT * FROM public.orders o WHERE o.id";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);
        List<SqlCompletionSuggestion> columns = request.Suggestions
            .Where(static s => s.Kind == SqlCompletionKind.Column)
            .ToList();

        Assert.NotEmpty(columns);
        Assert.Equal("id", columns[0].Label);
    }

    [Fact]
    public void GetSuggestions_WithMalformedComment_DoesNotThrow()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT /* unfinished comment WHERE o.";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.NotNull(request);
        Assert.NotEmpty(request.Suggestions);
    }

    [Fact]
    public void GetSuggestions_WithLargeMetadata_WarmRequestsStayFastAndStable()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildLargeMetadata(tableCount: 700, viewCount: 180);
        const string sql = "SELECT * FROM public.table_0100 t JOIN public.table_0099 p ON t.parent_id = p.id WHERE t.";

        var coldStopwatch = Stopwatch.StartNew();
        SqlCompletionRequest coldRequest = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);
        coldStopwatch.Stop();

        Assert.Contains(coldRequest.Suggestions, s => s.Kind == SqlCompletionKind.Column && s.Label == "id");

        const int iterations = 50;
        var warmStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);
            Assert.NotEmpty(request.Suggestions);
        }

        warmStopwatch.Stop();

        double coldMs = Math.Max(1, coldStopwatch.Elapsed.TotalMilliseconds);
        double warmAvgMs = warmStopwatch.Elapsed.TotalMilliseconds / iterations;

        Assert.True(
            warmAvgMs <= coldMs,
            $"Expected warm average ({warmAvgMs:F2}ms) to be <= cold call ({coldMs:F2}ms)."
        );
        Assert.True(
            warmStopwatch.Elapsed <= TimeSpan.FromSeconds(6),
            $"Warm completion loop exceeded expected budget: {warmStopwatch.Elapsed.TotalMilliseconds:F0}ms"
        );
    }

    [Fact]
    public void GetSuggestions_WithLargeMetadata_TypingSequenceRemainsResponsive()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildLargeMetadata(tableCount: 650, viewCount: 120);

        string[] typingFrames =
        [
            "S",
            "SE",
            "SEL",
            "SELE",
            "SELEC",
            "SELECT",
            "SELECT ",
            "SELECT * ",
            "SELECT * FROM ",
            "SELECT * FROM public.table_0001 t WHERE t.",
            "SELECT * FROM public.table_0001 t WHERE t.cr",
            "SELECT * FROM public.table_0001 t WHERE t.crea",
            "SELECT * FROM public.table_0001 t JOIN public.table_0002 p ON t.parent_id = p.id WHERE p."
        ];

        SqlCompletionRequest? lastRequest = null;
        var stopwatch = Stopwatch.StartNew();
        for (int round = 0; round < 12; round++)
        {
            foreach (string frame in typingFrames)
            {
                lastRequest = sut.GetSuggestions(frame, frame.Length, metadata, DatabaseProvider.Postgres);
                Assert.NotEmpty(lastRequest.Suggestions);
            }
        }

        stopwatch.Stop();

        Assert.NotNull(lastRequest);
        Assert.Contains(lastRequest!.Suggestions, s => s.Kind == SqlCompletionKind.Column && s.Label == "created_at");
        Assert.True(
            stopwatch.Elapsed <= TimeSpan.FromSeconds(8),
            $"Typing scenario exceeded expected budget: {stopwatch.Elapsed.TotalMilliseconds:F0}ms"
        );
    }

    private static DbMetadata BuildLargeMetadata(int tableCount, int viewCount)
    {
        var tables = new List<TableMetadata>(tableCount + viewCount);
        var foreignKeys = new List<ForeignKeyRelation>(Math.Max(0, tableCount - 1));

        for (int i = 1; i <= tableCount; i++)
        {
            string tableName = $"table_{i:D4}";
            var columns = new List<ColumnMetadata>
            {
                new("id", "int", "int", false, true, false, true, true, 1),
                new("name", "text", "text", true, false, false, false, false, 2),
                new("created_at", "timestamp", "timestamp", true, false, false, false, false, 3),
            };

            var outbound = new List<ForeignKeyRelation>();
            if (i > 1)
            {
                string parentTableName = $"table_{i - 1:D4}";
                columns.Add(new ColumnMetadata("parent_id", "int", "int", true, false, true, false, true, 4));

                var fk = new ForeignKeyRelation(
                    ConstraintName: $"fk_{tableName}_{parentTableName}",
                    ChildSchema: "public",
                    ChildTable: tableName,
                    ChildColumn: "parent_id",
                    ParentSchema: "public",
                    ParentTable: parentTableName,
                    ParentColumn: "id",
                    OnDelete: ReferentialAction.NoAction,
                    OnUpdate: ReferentialAction.NoAction,
                    OrdinalPosition: 1);

                foreignKeys.Add(fk);
                outbound.Add(fk);
            }

            tables.Add(new TableMetadata(
                Schema: "public",
                Name: tableName,
                Kind: TableKind.Table,
                EstimatedRowCount: 1_000 + i,
                Columns: columns,
                Indexes: [],
                OutboundForeignKeys: outbound,
                InboundForeignKeys: []));
        }

        for (int i = 1; i <= viewCount; i++)
        {
            string viewName = $"view_{i:D4}";
            tables.Add(new TableMetadata(
                Schema: "public",
                Name: viewName,
                Kind: TableKind.View,
                EstimatedRowCount: 0,
                Columns:
                [
                    new ColumnMetadata("id", "int", "int", true, false, false, false, false, 1),
                    new ColumnMetadata("name", "text", "text", true, false, false, false, false, 2),
                ],
                Indexes: [],
                OutboundForeignKeys: [],
                InboundForeignKeys: []));
        }

        return new DbMetadata(
            DatabaseName: "large_test_db",
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "16",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata("public", tables)],
            AllForeignKeys: foreignKeys);
    }

    private static DbMetadata BuildMetadata()
    {
        var fk = new ForeignKeyRelation(
            ConstraintName: "fk_orders_customers",
            ChildSchema: "public",
            ChildTable: "orders",
            ChildColumn: "customer_id",
            ParentSchema: "public",
            ParentTable: "customers",
            ParentColumn: "id",
            OnDelete: ReferentialAction.NoAction,
            OnUpdate: ReferentialAction.NoAction,
            OrdinalPosition: 1);

        var orderColumns = new List<ColumnMetadata>
        {
            new("id", "int", "int", false, true, false, true, true, 1),
            new("status", "text", "text", true, false, false, false, false, 2),
            new("customer_id", "int", "int", true, false, true, false, true, 3),
            new("user_id", "int", "int", true, false, true, false, true, 4),
            new("created_at", "timestamp", "timestamp", true, false, false, false, false, 5),
        };

        var customerColumns = new List<ColumnMetadata>
        {
            new("id", "int", "int", false, true, false, true, true, 1),
            new("full_name", "text", "text", false, false, false, false, false, 2),
        };

        var orders = new TableMetadata(
            Schema: "public",
            Name: "orders",
            Kind: TableKind.Table,
            EstimatedRowCount: 1000,
            Columns: orderColumns,
            Indexes: [],
            OutboundForeignKeys: [fk],
            InboundForeignKeys: []);

        var customers = new TableMetadata(
            Schema: "public",
            Name: "customers",
            Kind: TableKind.Table,
            EstimatedRowCount: 200,
            Columns: customerColumns,
            Indexes: [],
            OutboundForeignKeys: [],
            InboundForeignKeys: [fk]);

        var users = new TableMetadata(
            Schema: "public",
            Name: "users",
            Kind: TableKind.Table,
            EstimatedRowCount: 1200,
            Columns:
            [
                new ColumnMetadata("id", "int", "int", false, true, false, true, true, 1),
                new ColumnMetadata("full_name", "text", "text", true, false, false, false, false, 2),
            ],
            Indexes: [],
            OutboundForeignKeys: [],
            InboundForeignKeys: []);

        return new DbMetadata(
            DatabaseName: "testdb",
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "16",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata("public", [orders, customers, users])],
            AllForeignKeys: [fk]);
    }
}

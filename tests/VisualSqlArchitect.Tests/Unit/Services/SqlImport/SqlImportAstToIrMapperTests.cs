using DBWeaver.Core;
using DBWeaver.SqlImport.Contracts;
using DBWeaver.SqlImport.IR;
using DBWeaver.SqlImport.IR.Expressions;
using DBWeaver.SqlImport.IR.Sources;
using DBWeaver.UI.Services.SqlImport.Execution.Parsing;
using DBWeaver.UI.Services.SqlImport.Mapping;

namespace DBWeaver.Tests.Unit.Services.SqlImport;

public sealed class SqlImportAstToIrMapperTests
{
    [Fact]
    public void MapSelectFrom_WithSimpleProjection_BuildsRootIrContracts()
    {
        var mapper = new SqlImportAstToIrMapper();
        SqlImportParsedQuery parsed = CreateQuery(
            selectedColumns: [new SqlImportSelectedColumn("o.id", "order_id")],
            fromParts: [new SqlImportSourcePart("public.orders", "o", null, null)]
        );

        var ir = mapper.MapSelectFrom(parsed, "SELECT o.id AS order_id FROM public.orders o", DatabaseProvider.Postgres);

        Assert.Equal("1.0.0", ir.IrVersion);
        Assert.Single(ir.Query.SelectItems);
        Assert.IsType<TableRefSourceExpr>(ir.Query.FromSource);
        Assert.IsType<ColumnRefExpr>(ir.Query.SelectItems[0].Expression);
        Assert.Equal(SqlResolutionStatus.Resolved, ir.Query.SelectItems[0].Expression.ResolutionStatus);
    }

    [Fact]
    public void MapSelectFrom_WithSameInput_GeneratesDeterministicQueryId()
    {
        var mapper = new SqlImportAstToIrMapper();
        SqlImportParsedQuery parsed = CreateQuery(
            selectedColumns: [new SqlImportSelectedColumn("id", null)],
            fromParts: [new SqlImportSourcePart("orders", null, null, null)]
        );

        var first = mapper.MapSelectFrom(parsed, "SELECT id FROM orders", DatabaseProvider.SqlServer);
        var second = mapper.MapSelectFrom(parsed, "SELECT id FROM orders", DatabaseProvider.SqlServer);

        Assert.Equal(first.QueryId, second.QueryId);
        Assert.Equal(first.SourceHash, second.SourceHash);
    }

    [Fact]
    public void MapSelectFrom_WithJoinAndWhere_MapsJoinAndWhereExpressions()
    {
        var mapper = new SqlImportAstToIrMapper();
        SqlImportParsedQuery parsed = CreateQuery(
            selectedColumns: [new SqlImportSelectedColumn("o.id", null)],
            fromParts:
            [
                new SqlImportSourcePart("orders", "o", null, null),
                new SqlImportSourcePart("customers", "c", "INNER JOIN", "o.customer_id = c.id"),
            ],
            whereClause: "o.id > 10"
        );

        SqlToNodeIR ir = mapper.MapSelectFrom(
            parsed,
            "SELECT o.id FROM orders o INNER JOIN customers c ON o.customer_id = c.id WHERE o.id > 10",
            DatabaseProvider.Postgres
        );

        Assert.Single(ir.Query.Joins);
        Assert.NotNull(ir.Query.WhereExpr);
        Assert.IsType<DBWeaver.SqlImport.IR.Expressions.ComparisonExpr>(ir.Query.Joins[0].OnExpr);
        Assert.IsType<DBWeaver.SqlImport.IR.Expressions.ComparisonExpr>(ir.Query.WhereExpr);
    }

    [Fact]
    public void MapSelectFrom_WithUnqualifiedWhereAcrossMultipleSources_EmitsAmbiguousDiagnostic()
    {
        var mapper = new SqlImportAstToIrMapper();
        SqlImportParsedQuery parsed = CreateQuery(
            selectedColumns: [new SqlImportSelectedColumn("o.id", null)],
            fromParts:
            [
                new SqlImportSourcePart("orders", "o", null, null),
                new SqlImportSourcePart("customers", "c", "INNER JOIN", "o.customer_id = c.id"),
            ],
            whereClause: "id > 10"
        );

        SqlToNodeIR ir = mapper.MapSelectFrom(
            parsed,
            "SELECT o.id FROM orders o INNER JOIN customers c ON o.customer_id = c.id WHERE id > 10",
            DatabaseProvider.Postgres
        );

        Assert.Contains(ir.Diagnostics, diagnostic => diagnostic.Code == "SQLIMP_0201_COLUMN_AMBIGUOUS");
        Assert.NotNull(ir.Query.WhereExpr);
        Assert.Equal(SqlResolutionStatus.Ambiguous, ir.Query.WhereExpr!.ResolutionStatus);
    }

    [Fact]
    public void MapSelectFrom_WithGroupByAndHaving_MapsClauseExpressions()
    {
        var mapper = new SqlImportAstToIrMapper();
        SqlImportParsedQuery parsed = CreateQuery(
            selectedColumns:
            [
                new SqlImportSelectedColumn("o.customer_id", "customer_id"),
                new SqlImportSelectedColumn("COUNT(*)", "total_orders"),
            ],
            fromParts: [new SqlImportSourcePart("orders", "o", null, null)],
            groupBy: "o.customer_id",
            havingClause: "COUNT(*) > 1"
        );

        SqlToNodeIR ir = mapper.MapSelectFrom(
            parsed,
            "SELECT o.customer_id, COUNT(*) AS total_orders FROM orders o GROUP BY o.customer_id HAVING COUNT(*) > 1",
            DatabaseProvider.Postgres
        );

        Assert.Single(ir.Query.GroupBy);
        Assert.NotNull(ir.Query.HavingExpr);
        Assert.IsType<ColumnRefExpr>(ir.Query.GroupBy[0]);
        Assert.IsType<DBWeaver.SqlImport.IR.Expressions.ComparisonExpr>(ir.Query.HavingExpr);
    }

    [Fact]
    public void MapSelectFrom_WithOrderBy_MapsDirectionAndExpression()
    {
        var mapper = new SqlImportAstToIrMapper();
        SqlImportParsedQuery parsed = CreateQuery(
            selectedColumns: [new SqlImportSelectedColumn("o.id", "order_id")],
            fromParts: [new SqlImportSourcePart("orders", "o", null, null)],
            orderBy: "o.id DESC"
        );

        SqlToNodeIR ir = mapper.MapSelectFrom(
            parsed,
            "SELECT o.id AS order_id FROM orders o ORDER BY o.id DESC",
            DatabaseProvider.Postgres
        );

        Assert.Single(ir.Query.OrderBy);
        Assert.True(ir.Query.OrderBy[0].Descending);
        Assert.IsType<ColumnRefExpr>(ir.Query.OrderBy[0].Expression);
    }

    [Fact]
    public void MapSelectFrom_WithUnknownOrderByAlias_EmitsUnresolvedDiagnostic()
    {
        var mapper = new SqlImportAstToIrMapper();
        SqlImportParsedQuery parsed = CreateQuery(
            selectedColumns: [new SqlImportSelectedColumn("o.id", "order_id")],
            fromParts: [new SqlImportSourcePart("orders", "o", null, null)],
            orderBy: "x.id"
        );

        SqlToNodeIR ir = mapper.MapSelectFrom(
            parsed,
            "SELECT o.id AS order_id FROM orders o ORDER BY x.id",
            DatabaseProvider.Postgres
        );

        Assert.Contains(ir.Diagnostics, diagnostic => diagnostic.Code == "SQLIMP_0202_COLUMN_UNRESOLVED");
        Assert.Single(ir.Query.OrderBy);
        Assert.Equal(SqlResolutionStatus.Unresolved, ir.Query.OrderBy[0].ResolutionStatus);
    }

    [Fact]
    public void MapSelectFrom_WithTopSyntax_PreservesTopSemanticsInLimitExpr()
    {
        var mapper = new SqlImportAstToIrMapper();
        SqlImportParsedQuery parsed = CreateQuery(
            selectedColumns: [new SqlImportSelectedColumn("o.id", null)],
            fromParts: [new SqlImportSourcePart("orders", "o", null, null)],
            limit: 10
        );

        SqlToNodeIR ir = mapper.MapSelectFrom(
            parsed,
            "SELECT TOP 10 o.id FROM orders o",
            DatabaseProvider.SqlServer
        );

        Assert.NotNull(ir.Query.LimitOrTop);
        Assert.Equal(10, ir.Query.LimitOrTop!.Value);
        Assert.True(ir.Query.LimitOrTop.IsTopSyntax);
    }

    [Fact]
    public void MapSelectFrom_WithUnion_MapsRightQueryAsSetOperation()
    {
        var mapper = new SqlImportAstToIrMapper();
        SqlImportParsedQuery parsed = CreateQuery(
            selectedColumns: [new SqlImportSelectedColumn("o.id", null)],
            fromParts: [new SqlImportSourcePart("orders", "o", null, null)]
        );

        SqlToNodeIR ir = mapper.MapSelectFrom(
            parsed,
            "SELECT o.id FROM orders o UNION SELECT a.id FROM archived_orders a",
            DatabaseProvider.Postgres
        );

        Assert.Single(ir.Query.SetOperations);
        Assert.Equal("UNION", ir.Query.SetOperations[0].Kind);
        Assert.False(ir.Query.SetOperations[0].IsAll);
        Assert.Single(ir.Query.SetOperations[0].RightQuery.SelectItems);
    }

    [Fact]
    public void MapSelectFrom_WithUnionAll_MapsSetOperationAsAll()
    {
        var mapper = new SqlImportAstToIrMapper();
        SqlImportParsedQuery parsed = CreateQuery(
            selectedColumns: [new SqlImportSelectedColumn("o.id", null)],
            fromParts: [new SqlImportSourcePart("orders", "o", null, null)]
        );

        SqlToNodeIR ir = mapper.MapSelectFrom(
            parsed,
            "SELECT o.id FROM orders o UNION ALL SELECT a.id FROM archived_orders a",
            DatabaseProvider.Postgres
        );

        Assert.Single(ir.Query.SetOperations);
        Assert.Equal("UNION", ir.Query.SetOperations[0].Kind);
        Assert.True(ir.Query.SetOperations[0].IsAll);
    }

    [Fact]
    public void MapSelectFrom_WithIntersect_EmitsUnsupportedSetOperationDiagnostic()
    {
        var mapper = new SqlImportAstToIrMapper();
        SqlImportParsedQuery parsed = CreateQuery(
            selectedColumns: [new SqlImportSelectedColumn("o.id", null)],
            fromParts: [new SqlImportSourcePart("orders", "o", null, null)]
        );

        SqlToNodeIR ir = mapper.MapSelectFrom(
            parsed,
            "SELECT o.id FROM orders o INTERSECT SELECT a.id FROM archived_orders a",
            DatabaseProvider.Postgres
        );

        Assert.Empty(ir.Query.SetOperations);
        Assert.Contains(ir.Diagnostics, diagnostic =>
            diagnostic.Code == "SQLIMP_0002_AST_UNSUPPORTED"
            && diagnostic.Message.Contains("INTERSECT", StringComparison.OrdinalIgnoreCase));
    }

    private static SqlImportParsedQuery CreateQuery(
        IReadOnlyList<SqlImportSelectedColumn> selectedColumns,
        IReadOnlyList<SqlImportSourcePart> fromParts,
        string? whereClause = null,
        string? orderBy = null,
        string? groupBy = null,
        string? havingClause = null,
        int? limit = null
    )
    {
        return new SqlImportParsedQuery(
            IsDistinct: false,
            IsStar: false,
            StarQualifier: null,
            SelectedColumns: selectedColumns,
            FromParts: fromParts,
            WhereClause: whereClause,
            OrderBy: orderBy,
            GroupBy: groupBy,
            HavingClause: havingClause,
            Limit: limit,
            OuterAliases: []
        );
    }
}

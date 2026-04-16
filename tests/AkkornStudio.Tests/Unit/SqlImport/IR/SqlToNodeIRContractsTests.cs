using System.Text.Json;
using AkkornStudio.SqlImport.Contracts;
using AkkornStudio.SqlImport.Diagnostics;
using AkkornStudio.SqlImport.Ids;
using AkkornStudio.SqlImport.IR;
using AkkornStudio.SqlImport.IR.Expressions;
using AkkornStudio.SqlImport.IR.Metadata;
using AkkornStudio.SqlImport.IR.Sources;
using AkkornStudio.SqlImport.Semantics.SymbolTable;
using AkkornStudio.SqlImport.Tracing;

namespace AkkornStudio.Tests.Unit.SqlImport.IR;

public sealed class SqlToNodeIRContractsTests
{
    [Fact]
    public void SqlToNodeIr_WithMinimalValidGraph_PreservesRequiredContracts()
    {
        SqlToNodeIR model = CreateMinimalIr();

        Assert.False(string.IsNullOrWhiteSpace(model.IrVersion));
        Assert.False(string.IsNullOrWhiteSpace(model.QueryId));
        Assert.NotNull(model.Query.FromSource);
        Assert.NotEmpty(model.Query.SelectItems);
        Assert.NotNull(model.SymbolTable);
        Assert.NotNull(model.IdGenerationMeta);
    }

    [Fact]
    public void SqlToNodeIr_SerializesDeterministically_ForSameInput()
    {
        SqlToNodeIR first = CreateMinimalIr();
        SqlToNodeIR second = CreateMinimalIr();

        string firstJson = JsonSerializer.Serialize(first);
        string secondJson = JsonSerializer.Serialize(second);

        Assert.Equal(firstJson, secondJson);
    }

    private static SqlToNodeIR CreateMinimalIr()
    {
        var span = new SourceSpan(1, 1, 1, 10, "frag_hash");
        var trace = new TraceMeta("query_1", "expr_1", "corr_1", span);
        var nodeMetadata = new SqlIrNodeMetadata(false, null, [], []);

        var fromSource = new TableRefSourceExpr(
            "source_1",
            null,
            "dbo",
            "orders",
            "o",
            SqlResolutionStatus.Resolved,
            nodeMetadata
        );

        var columnRef = new ColumnRefExpr(
            "expr_1",
            span,
            SqlImportSemanticType.Integer,
            SqlResolutionStatus.Resolved,
            trace,
            nodeMetadata,
            "o",
            "id",
            "source_1"
        );

        var aliasMeta = new AliasMeta("Id", "id", "Id", "active_convention", []);

        var selectItem = new SelectItemExpr(
            "select_1",
            columnRef,
            aliasMeta,
            0,
            SqlImportSemanticType.Integer,
            span,
            nodeMetadata
        );

        var query = new QueryExpr(
            [selectItem],
            fromSource,
            [],
            null,
            [],
            null,
            [],
            null,
            []
        );

        var symbolTable = new SymbolTableModel(
            [
                new Scope(
                    "scope_root",
                    ScopeType.Root,
                    null,
                    new Dictionary<string, IReadOnlyList<SourceSymbol>>
                    {
                        ["o"] = [new SourceSymbol("source_1", "o", "o", "dbo", "orders", "o")],
                    },
                    new Dictionary<string, IReadOnlyList<ProjectionSymbol>>
                    {
                        ["id"] = [new ProjectionSymbol("select_1", "id", "id", 0)],
                    }
                ),
            ]
        );

        return new SqlToNodeIR(
            "1.0.0",
            "query_1",
            "source_hash_1",
            SqlImportDialect.SqlServer,
            ["SqlImport.AstIrPrimary"],
            query,
            symbolTable,
            [
                new SqlImportDiagnostic(
                    "SQLIMP_0101_ALIAS_NORMALIZATION_LOSS",
                    SqlImportDiagnosticCategory.NormalizationLoss,
                    SqlImportDiagnosticSeverity.Info,
                    "Alias normalized.",
                    SqlImportClause.Select,
                    span,
                    "SELECT o.id AS Id",
                    SqlImportDiagnosticAction.ContinuePartial,
                    "No action required.",
                    "query_1",
                    "corr_1"
                ),
            ],
            new IrMetrics(1, 1, 0, 1, 0, 0, 0),
            StableSqlImportIdGenerator.CreateDefaultMeta()
        );
    }
}

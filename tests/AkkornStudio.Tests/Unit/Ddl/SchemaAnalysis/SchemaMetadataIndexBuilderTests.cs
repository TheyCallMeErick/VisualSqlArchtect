using AkkornStudio.Ddl.SchemaAnalysis.Application.Indexing;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;
using AkkornStudio.Ddl.SchemaAnalysis.Domain.Enums;
using AkkornStudio.Metadata;

namespace AkkornStudio.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaMetadataIndexBuilderTests
{
    [Fact]
    public void Build_CreatesDeterministicIndexes_ByCanonicalTableName()
    {
        SchemaMetadataIndexBuilder builder = new();
        DbMetadata metadata = CreateMetadata();
        SchemaAnalysisProfile profile = CreateProfile();

        SchemaMetadataIndexSnapshot first = builder.Build(metadata, profile);
        SchemaMetadataIndexSnapshot second = builder.Build(metadata, profile);

        Assert.True(first.TableByFullName.ContainsKey("public.orders"));
        Assert.Equal(first.TableByFullName.Keys, second.TableByFullName.Keys);
        Assert.Equal(TableKind.Table, first.TableKindsByFullName["public.orders"]);
        Assert.Equal(["id"], first.PkColumnsByTable["public.orders"].Select(static column => column.Name));
    }

    [Fact]
    public void Build_CreatesForeignKeyIndexes_ByChildAndParentTable()
    {
        SchemaMetadataIndexBuilder builder = new();

        SchemaMetadataIndexSnapshot snapshot = builder.Build(CreateMetadata(), CreateProfile());

        Assert.Single(snapshot.FkByChildTable["public.orders"]);
        Assert.Single(snapshot.FkByParentTable["public.customers"]);
        Assert.Contains("fk_orders_customer", snapshot.ConstraintNamesBySchema["public"]);
    }

    [Fact]
    public void Build_CreatesNormalizedNameIndex_ForTablesColumnsAndConstraints()
    {
        SchemaMetadataIndexBuilder builder = new();

        SchemaMetadataIndexSnapshot snapshot = builder.Build(CreateMetadata(), CreateProfile());

        SchemaNormalizedNameIndexEntry tableEntry = snapshot.NormalizedNameIndex["table|public.orders"];
        SchemaNormalizedNameIndexEntry columnEntry = snapshot.NormalizedNameIndex["column|public.orders.customer_id"];
        SchemaNormalizedNameIndexEntry constraintEntry = snapshot.NormalizedNameIndex["constraint|public|fk_orders_customer"];

        Assert.Equal(["order"], tableEntry.Tokens.EntityTokens);
        Assert.Equal("customer", columnEntry.Tokens.PrincipalEntityToken);
        Assert.Equal(SchemaTargetType.Constraint, constraintEntry.TargetType);
        Assert.Contains("customer", constraintEntry.Tokens.EntityTokens);
    }

    [Fact]
    public void Build_InitializesRuleExecutionState_ForAllRules()
    {
        SchemaMetadataIndexBuilder builder = new();

        SchemaMetadataIndexSnapshot snapshot = builder.Build(CreateMetadata(), CreateProfile());

        Assert.Equal(Enum.GetValues<SchemaRuleCode>().Length, snapshot.RuleExecutionState.Count);
        Assert.All(snapshot.RuleExecutionState.Values, static state => Assert.Equal(RuleExecutionState.NotStarted, state));
    }

    private static DbMetadata CreateMetadata()
    {
        ForeignKeyRelation foreignKey = new(
            ConstraintName: "fk_orders_customer",
            ChildSchema: "public",
            ChildTable: "orders",
            ChildColumn: "customer_id",
            ParentSchema: "public",
            ParentTable: "customers",
            ParentColumn: "id",
            OnDelete: ReferentialAction.NoAction,
            OnUpdate: ReferentialAction.NoAction
        );

        TableMetadata customers = new(
            Schema: "public",
            Name: "customers",
            Kind: TableKind.Table,
            EstimatedRowCount: 10,
            Columns:
            [
                new ColumnMetadata("id", "integer", "integer", false, true, false, true, true, 1),
                new ColumnMetadata("name", "text", "text", false, false, false, false, false, 2),
            ],
            Indexes:
            [
                new IndexMetadata("uq_customers_name", true, false, false, ["name"]),
            ],
            OutboundForeignKeys: [],
            InboundForeignKeys: [foreignKey],
            Comment: "Customers"
        );

        TableMetadata orders = new(
            Schema: "public",
            Name: "orders",
            Kind: TableKind.Table,
            EstimatedRowCount: 100,
            Columns:
            [
                new ColumnMetadata("id", "integer", "integer", false, true, false, false, true, 1),
                new ColumnMetadata("customer_id", "integer", "integer", false, false, true, false, true, 2),
            ],
            Indexes:
            [
                new IndexMetadata("ix_orders_customer_id", false, false, false, ["customer_id"]),
            ],
            OutboundForeignKeys: [foreignKey],
            InboundForeignKeys: [],
            Comment: "Orders"
        );

        return new DbMetadata(
            DatabaseName: "db",
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "16",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata("public", [customers, orders])],
            AllForeignKeys: [foreignKey]
        );
    }

    private static SchemaAnalysisProfile CreateProfile() =>
        new(
            Version: 1,
            Enabled: true,
            MinConfidenceGlobal: 0.55,
            TimeoutMs: 15000,
            AllowPartialOnTimeout: true,
            AllowPartialOnRuleFailure: true,
            EnableParallelRules: true,
            MaxDegreeOfParallelism: 4,
            MaxIssues: 5000,
            MaxSuggestionsPerIssue: 3,
            NamingConvention: NamingConvention.SnakeCase,
            NormalizationStrictness: NormalizationStrictness.Balanced,
            RequiredCommentTargets: ["Table"],
            LowQualityNameDenylist: ["tmp"],
            NameAllowlist: [],
            SynonymGroups: [new List<string> { "customer", "cliente" }],
            SemiStructuredPayloadAllowlist: [],
            DebugDiagnostics: false,
            RuleSettings: new Dictionary<SchemaRuleCode, SchemaRuleSetting>
            {
                [SchemaRuleCode.MISSING_FK] = new SchemaRuleSetting(true, 0.65, 1000),
            },
            CacheTtlSeconds: 300
        );
}

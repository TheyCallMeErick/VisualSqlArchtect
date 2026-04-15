using DBWeaver.Core;
using DBWeaver.Ddl.SchemaAnalysis.Application.Indexing;
using DBWeaver.Ddl.SchemaAnalysis.Application.Rules;
using DBWeaver.Ddl.SchemaAnalysis.Application.Validation;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;
using DBWeaver.Metadata;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class MissingFkRuleTests
{
    private readonly SchemaMetadataIndexBuilder _indexBuilder = new();
    private readonly MissingFkRule _rule = new();

    [Fact]
    public async Task ExecuteAsync_EmitsStrongIssue_ForCanonicalCustomerIdPattern()
    {
        DbMetadata metadata = CreateMetadata(
            sourceColumn: CreateColumn("customer_id", "integer", isIndexed: true),
            targetTables:
            [
                CreateTargetTable("customers", "id", isPrimaryKey: true),
            ]
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.Single(result.Issues);
        Assert.Equal(SchemaIssueSeverity.Critical, result.Issues[0].Severity);
        Assert.False(result.Issues[0].IsAmbiguous);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotEmit_WhenScoreIsBelowThreshold()
    {
        SchemaAnalysisProfile profile = SchemaAnalysisProfileNormalizer.CreateDefaultProfile() with
        {
            RuleSettings = SchemaAnalysisProfileNormalizer.CreateDefaultProfile().RuleSettings
                .ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Key == SchemaRuleCode.MISSING_FK
                        ? new SchemaRuleSetting(true, 0.80, 1000)
                        : pair.Value
                ),
        };

        DbMetadata metadata = CreateMetadata(
            sourceColumn: CreateColumn("customer_ref", "integer", isIndexed: false),
            targetTables:
            [
                CreateTargetTable(
                    "customers",
                    "customer_code",
                    isPrimaryKey: false,
                    uniqueColumns: ["customer_code"]
                ),
            ]
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata, profile));

        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotEmit_WhenEntityIsNotInferable()
    {
        DbMetadata metadata = CreateMetadata(
            sourceColumn: CreateColumn("id_ref_code", "integer", isIndexed: true),
            targetTables:
            [
                CreateTargetTable("customers", "id", isPrimaryKey: true),
            ]
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotEmit_WhenTypeCompatibilityIsNotExactOrStrong()
    {
        DbMetadata metadata = CreateMetadata(
            sourceColumn: CreateColumn("customer_id", "bit", isIndexed: true),
            targetTables:
            [
                CreateTargetTable("customers", "id", isPrimaryKey: true),
            ],
            provider: DatabaseProvider.SqlServer
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata));

        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ExecuteAsync_MarksIssueAsAmbiguous_WhenTopCandidatesStayTooClose()
    {
        SchemaAnalysisProfile profile = SchemaAnalysisProfileNormalizer.CreateDefaultProfile() with
        {
            MinConfidenceGlobal = 0.50,
            SynonymGroups =
            [
                ["person", "people", "personas"],
            ],
            RuleSettings = SchemaAnalysisProfileNormalizer.CreateDefaultProfile().RuleSettings
                .ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Key == SchemaRuleCode.MISSING_FK
                        ? new SchemaRuleSetting(true, 0.50, 1000)
                        : pair.Value
                ),
        };

        DbMetadata metadata = CreateMetadata(
            sourceColumn: CreateColumn("person_id", "integer", isIndexed: true),
            targetTables:
            [
                CreateTargetTable("people", "id", isPrimaryKey: true),
                CreateTargetTable("personas", "id", isPrimaryKey: true),
            ]
        );

        SchemaRuleExecutionResult result = await _rule.ExecuteAsync(CreateContext(metadata, profile));

        Assert.Single(result.Issues);
        Assert.True(result.Issues[0].IsAmbiguous);
        Assert.Equal(SchemaIssueSeverity.Warning, result.Issues[0].Severity);
    }

    private SchemaAnalysisExecutionContext CreateContext(
        DbMetadata metadata,
        SchemaAnalysisProfile? profile = null
    )
    {
        SchemaAnalysisProfile effectiveProfile = profile ?? SchemaAnalysisProfileNormalizer.CreateDefaultProfile();
        SchemaMetadataIndexSnapshot indices = _indexBuilder.Build(metadata, effectiveProfile);
        return new SchemaAnalysisExecutionContext(metadata, effectiveProfile, indices, "fingerprint", "profile");
    }

    private static DbMetadata CreateMetadata(
        ColumnMetadata sourceColumn,
        IReadOnlyList<TableMetadata> targetTables,
        DatabaseProvider provider = DatabaseProvider.Postgres
    )
    {
        string schema = provider == DatabaseProvider.MySql ? string.Empty : "public";

        TableMetadata sourceTable = new(
            Schema: schema,
            Name: "orders",
            Kind: TableKind.Table,
            EstimatedRowCount: 100,
            Columns:
            [
                CreateColumn("id", "integer", isPrimaryKey: true),
                sourceColumn with { OrdinalPosition = 2 },
            ],
            Indexes: sourceColumn.IsIndexed
                ? [new IndexMetadata($"ix_orders_{sourceColumn.Name}", false, false, false, [sourceColumn.Name])]
                : [],
            OutboundForeignKeys: [],
            InboundForeignKeys: [],
            Comment: "Orders"
        );

        List<TableMetadata> allTables = [sourceTable];
        allTables.AddRange(targetTables.Select(table => table with { Schema = schema }));

        return new DbMetadata(
            DatabaseName: "db",
            Provider: provider,
            ServerVersion: "1",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata(schema, allTables)],
            AllForeignKeys: []
        );
    }

    private static TableMetadata CreateTargetTable(
        string tableName,
        string targetColumnName,
        bool isPrimaryKey,
        IReadOnlyList<string>? uniqueColumns = null
    )
    {
        List<IndexMetadata> indexes = [];
        if (uniqueColumns is not null)
        {
            indexes.Add(new IndexMetadata($"uq_{tableName}_{uniqueColumns[0]}", true, false, false, uniqueColumns));
        }

        return new TableMetadata(
            Schema: "public",
            Name: tableName,
            Kind: TableKind.Table,
            EstimatedRowCount: 10,
            Columns:
            [
                CreateColumn(targetColumnName, "integer", isPrimaryKey: isPrimaryKey),
            ],
            Indexes: indexes,
            OutboundForeignKeys: [],
            InboundForeignKeys: [],
            Comment: tableName
        );
    }

    private static ColumnMetadata CreateColumn(
        string name,
        string nativeType,
        bool isPrimaryKey = false,
        bool isIndexed = false
    ) =>
        new(
            Name: name,
            DataType: nativeType,
            NativeType: nativeType,
            IsNullable: false,
            IsPrimaryKey: isPrimaryKey,
            IsForeignKey: false,
            IsUnique: false,
            IsIndexed: isIndexed,
            OrdinalPosition: 1,
            Comment: name
        );
}

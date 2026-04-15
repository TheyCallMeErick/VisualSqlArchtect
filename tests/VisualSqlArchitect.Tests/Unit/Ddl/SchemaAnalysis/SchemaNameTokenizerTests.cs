using DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;
using DBWeaver.Ddl.SchemaAnalysis.Domain.Normalization;

namespace DBWeaver.Tests.Unit.Ddl.SchemaAnalysis;

public sealed class SchemaNameTokenizerTests
{
    [Theory]
    [InlineData("PessoaId")]
    [InlineData("id_pessoa")]
    [InlineData("IdPessoa")]
    [InlineData("pessoa-id")]
    public void Tokenize_InfersEntityPessoa_ForCanonicalIdPatterns(string raw)
    {
        SchemaNameTokenizer tokenizer = new();

        NormalizedNameTokens tokens = tokenizer.Tokenize(raw, CreateProfile());

        Assert.Contains("person", tokens.EntityTokens);
        Assert.Contains("id", tokens.StructuralTokens);
        Assert.Equal("person", tokens.PrincipalEntityToken);
    }

    [Fact]
    public void Tokenize_HandlesDiacriticsAndAcronymPlural_Correctly()
    {
        SchemaNameTokenizer tokenizer = new();

        NormalizedNameTokens tokens = tokenizer.Tokenize("ÓrgãosIDs", CreateProfile());

        Assert.Equal(["orgao", "id"], tokens.AllTokens);
        Assert.Equal(["id"], tokens.StructuralTokens);
        Assert.Equal(["orgao"], tokens.EntityTokens);
        Assert.Equal("orgao", tokens.PrincipalEntityToken);
    }

    [Fact]
    public void Tokenize_SplitsCamelAndPascalNames_Deterministically()
    {
        SchemaNameTokenizer tokenizer = new();

        NormalizedNameTokens first = tokenizer.Tokenize("CustomerName", CreateProfile());
        NormalizedNameTokens second = tokenizer.Tokenize("CustomerName", CreateProfile());

        Assert.Equal(["customer", "name"], first.AllTokens);
        Assert.Equal(first.AllTokens, second.AllTokens);
        Assert.Equal(first.StructuralTokens, second.StructuralTokens);
        Assert.Equal(first.EntityTokens, second.EntityTokens);
        Assert.Equal(first.PrincipalEntityToken, second.PrincipalEntityToken);
    }

    [Fact]
    public void Tokenize_AppliesSynonymGroups_AfterSingularization()
    {
        SchemaNameTokenizer tokenizer = new();
        SchemaAnalysisProfile profile = CreateProfile(
            synonymGroups:
            [
                new List<string> { "person", "pessoa" },
                new List<string> { "customer", "cliente" },
            ]
        );

        NormalizedNameTokens tokens = tokenizer.Tokenize("clientes_id", profile);

        Assert.Equal(["customer", "id"], tokens.AllTokens);
        Assert.Equal(["customer"], tokens.EntityTokens);
    }

    [Fact]
    public void Tokenize_ProducesNoEntityTokens_ForStructuralOnlyNames()
    {
        SchemaNameTokenizer tokenizer = new();

        NormalizedNameTokens tokens = tokenizer.Tokenize("id_fk_ref_code", CreateProfile());

        Assert.False(tokens.HasEntityTokens);
        Assert.Null(tokens.PrincipalEntityToken);
        Assert.Equal(["id", "fk", "ref", "code"], tokens.StructuralTokens);
    }

    private static SchemaAnalysisProfile CreateProfile(
        IReadOnlyList<IReadOnlyList<string>>? synonymGroups = null
    ) =>
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
            RequiredCommentTargets: ["Table", "PrimaryKeyColumn"],
            LowQualityNameDenylist: ["tmp"],
            NameAllowlist: [],
            SynonymGroups: synonymGroups ?? [new List<string> { "person", "pessoa" }],
            SemiStructuredPayloadAllowlist: [],
            DebugDiagnostics: false,
            RuleSettings: new Dictionary<SchemaRuleCode, SchemaRuleSetting>
            {
                [SchemaRuleCode.MISSING_FK] = new SchemaRuleSetting(true, 0.65, 1000),
            },
            CacheTtlSeconds: 300
        );
}

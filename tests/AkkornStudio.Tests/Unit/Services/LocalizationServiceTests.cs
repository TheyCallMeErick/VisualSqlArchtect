using AkkornStudio.UI.Services.Localization;
using Xunit;

namespace AkkornStudio.Tests.Unit.Services;

[Collection("LocalizationSensitive")]
public class LocalizationServiceTests
{
    [Fact]
    public void SetCulture_SwitchesBetweenPtBrAndEnUs()
    {
        var loc = LocalizationService.Instance;

        Assert.True(loc.SetCulture("pt-BR"));
        string ptLabel = loc["connection.connect"];

        Assert.True(loc.SetCulture("en-US"));
        string enLabel = loc["connection.connect"];

        Assert.NotEqual(ptLabel, enLabel);
        Assert.Equal("Connect", enLabel);

        // restore default
        loc.SetCulture("pt-BR");
    }

    [Fact]
    public void ToggleCulture_UpdatesLanguageLabel()
    {
        var loc = LocalizationService.Instance;

        loc.SetCulture("pt-BR");
        string before = loc.CurrentLanguageLabel;

        bool changed = loc.ToggleCulture();

        Assert.True(changed);
        Assert.NotEqual(before, loc.CurrentLanguageLabel);

        // restore default
        loc.SetCulture("pt-BR");
    }

    [Fact]
    public void SetCulture_SupportsExtendedLocales()
    {
        var loc = LocalizationService.Instance;

        Assert.True(loc.SetCulture("es-ES"));
        Assert.Equal("es-ES", loc.CurrentCulture);

        Assert.True(loc.SetCulture("ru-RU"));
        Assert.Equal("ru-RU", loc.CurrentCulture);

        Assert.True(loc.SetCulture("ja-JP"));
        Assert.Equal("ja-JP", loc.CurrentCulture);

        Assert.True(loc.SetCulture("zh-TW"));
        Assert.Equal("zh-TW", loc.CurrentCulture);

        // restore default
        loc.SetCulture("pt-BR");
    }

    [Fact]
    public void SchemaAnalysisKeys_AreAvailableInSupportedLocales()
    {
        var loc = LocalizationService.Instance;
        string[] cultures = ["pt-BR", "en-US", "es-ES", "ru-RU", "ja-JP"];
        string[] requiredKeys =
        [
            "preview.schemaAnalysis.run",
            "preview.schemaAnalysis.cancel",
            "preview.schemaAnalysis.clearFilters",
            "preview.schemaAnalysis.severity",
            "preview.schemaAnalysis.rule",
            "preview.schemaAnalysis.minConfidence",
            "preview.schemaAnalysis.tableFilter",
            "preview.schemaAnalysis.details",
            "preview.schemaAnalysis.evidence",
            "preview.schemaAnalysis.suggestions",
            "preview.schemaAnalysis.ruleDiagnostics",
            "preview.schemaAnalysis.sqlCandidates",
            "preview.schemaAnalysis.copySql",
            "preview.schemaAnalysis.applyToCanvas",
            "preview.schemaAnalysis.state.metadataUnavailable",
            "preview.schemaAnalysis.state.cancelled",
            "preview.schemaAnalysis.state.partialTimeout",
            "preview.schemaAnalysis.state.failed",
            "preview.schemaAnalysis.state.empty",
            "preview.schemaAnalysis.state.noFilterMatch",
            "preview.schemaAnalysis.state.noIssueSelected",
            "preview.schemaAnalysis.state.noSqlCandidate",
            "preview.schemaAnalysis.actionBlockedTooltip",
        ];

        foreach (string culture in cultures)
        {
            Assert.True(loc.SetCulture(culture));
            foreach (string key in requiredKeys)
            {
                string value = loc[key];
                Assert.NotEqual(key, value);
            }
        }

        // restore default
        loc.SetCulture("pt-BR");
    }
}

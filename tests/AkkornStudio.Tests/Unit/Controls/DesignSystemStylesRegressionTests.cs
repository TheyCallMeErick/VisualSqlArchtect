using System.IO;
using Xunit;

namespace AkkornStudio.Tests.Unit.Controls;

public class DesignSystemStylesRegressionTests
{
    [Fact]
    public void AppStyles_Defines_SharedProjectComponentStyles()
    {
        string styles = ReadRepoFile("src", "AkkornStudio.UI", "Assets", "Themes", "AppStyles.axaml");

        Assert.Contains("Style Selector=\"Border.surface-panel\"", styles);
        Assert.Contains("Style Selector=\"Border.surface-card\"", styles);
        Assert.Contains("Style Selector=\"Border.callout-subtle\"", styles);
        Assert.Contains("Style Selector=\"Border.badge\"", styles);
        Assert.Contains("Style Selector=\"TextBox.field\"", styles);
        Assert.Contains("Style Selector=\"ComboBox.field\"", styles);
        Assert.Contains("Style Selector=\"Button.tb\"", styles);
        Assert.Contains("Style Selector=\"Button.icon-ghost\"", styles);
        Assert.Contains("Style Selector=\"Button.panel-tab\"", styles);
        Assert.Contains("Style Selector=\"ToggleButton.secondary\"", styles);
        Assert.Contains("Style Selector=\"Expander.app-accordion\"", styles);
    }

    [Fact]
    public void CoreViews_Consume_SharedDesignSystemClasses()
    {
        string ddl = ReadRepoFile("src", "AkkornStudio.UI", "Controls", "Shell", "DdlSchemaAnalysisWorkspaceControl.axaml");
        string schemaBrowser = ReadRepoFile("src", "AkkornStudio.UI", "Controls", "SqlEditor", "SqlEditorSchemaBrowserControl.axaml");
        string diagnostics = ReadRepoFile("src", "AkkornStudio.UI", "Controls", "SidebarLeft", "SidebarDiagnosticsControl.axaml");
        string propertyPanel = ReadRepoFile("src", "AkkornStudio.UI", "Controls", "PropertyPanel", "PropertyPanelControl.axaml");

        Assert.Contains("Classes=\"surface-panel\"", ddl);
        Assert.Contains("Classes=\"app-accordion\"", ddl);
        Assert.Contains("Classes=\"secondary compact\"", ddl);

        Assert.Contains("Classes=\"field\"", schemaBrowser);
        Assert.Contains("Classes=\"app-accordion\"", schemaBrowser);
        Assert.Contains("Classes=\"badge info\"", schemaBrowser);

        Assert.Contains("Classes=\"app-accordion diag-category\"", diagnostics);
        Assert.Contains("Classes=\"badge danger\"", diagnostics);

        Assert.Contains("Classes=\"callout-subtle\"", propertyPanel);
        Assert.Contains("Classes=\"secondary compact\"", propertyPanel);
    }

    private static string ReadRepoFile(params string[] relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var segments = new string[relativePath.Length + 1];
            segments[0] = dir.FullName;
            for (int i = 0; i < relativePath.Length; i++)
                segments[i + 1] = relativePath[i];

            string candidate = Path.Combine(segments);
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(relativePath)} from test base directory.");
    }
}

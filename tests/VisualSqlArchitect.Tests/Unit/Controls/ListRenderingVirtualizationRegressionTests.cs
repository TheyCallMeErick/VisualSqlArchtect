using System.IO;

namespace DBWeaver.Tests.Unit.Controls;

public sealed class ListRenderingVirtualizationRegressionTests
{
    [Fact]
    public void CommandPalette_ResultsList_UsesVirtualizingStackPanel()
    {
        string xaml = ReadControlXaml("CommandPalette", "CommandPaletteControl.axaml");

        Assert.Contains("ItemsSource=\"{Binding Results}\"", xaml);
        Assert.Contains("Name=\"ResultsList\"", xaml);
        Assert.Contains("<VirtualizingStackPanel/>", xaml);
    }

    [Fact]
    public void SearchMenu_Lists_UseVirtualizingStackPanel()
    {
        string xaml = ReadControlXaml("SearchMenu", "SearchMenuControl.axaml");

        Assert.Contains("ItemsSource=\"{Binding Snippets}\"", xaml);
        Assert.Contains("Name=\"SnippetsList\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding Results}\"", xaml);
        Assert.Contains("Name=\"ResultsList\"", xaml);
        Assert.Contains("<VirtualizingStackPanel/>", xaml);
    }

    [Fact]
    public void AppDiagnostics_EntriesList_UsesVirtualizingStackPanel()
    {
        string xaml = ReadControlXaml("AppDiagnostics", "AppDiagnosticsControl.axaml");

        Assert.Contains("ItemsSource=\"{Binding Entries}\"", xaml);
        Assert.Contains("<VirtualizingStackPanel/>", xaml);
    }

    [Fact]
    public void FlowVersionOverlay_Lists_UseVirtualizingStackPanel()
    {
        string xaml = ReadControlXaml("FlowVersions", "FlowVersionOverlay.axaml");

        Assert.Contains("ItemsSource=\"{Binding Versions}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding DiffItems}\"", xaml);
        Assert.Contains("<VirtualizingStackPanel/>", xaml);
    }

    [Fact]
    public void SchemaControl_Lists_UseVirtualizingStackPanel()
    {
        string xaml = ReadControlXaml("SidebarLeft", "SchemaControl.axaml");

        Assert.Contains("ItemsSource=\"{Binding Categories}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding Items}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding Children}\"", xaml);
        Assert.Contains("<VirtualizingStackPanel/>", xaml);
    }

    [Fact]
    public void NodesListControl_GroupList_UsesVirtualizingStackPanel()
    {
        string xaml = ReadControlXaml("SidebarLeft", "NodesListControl.axaml");

        Assert.Contains("ItemsSource=\"{Binding FilteredGroups}\"", xaml);
        Assert.Contains("<VirtualizingStackPanel/>", xaml);
    }

    [Fact]
    public void SidebarDiagnostics_Lists_UseVirtualizingStackPanel()
    {
        string xaml = ReadControlXaml("SidebarLeft", "SidebarDiagnosticsControl.axaml");

        Assert.Contains("ItemsSource=\"{Binding Categories}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding Items}\"", xaml);
        Assert.Contains("<VirtualizingStackPanel/>", xaml);
    }

    [Fact]
    public void DdlSchemaAnalysis_GroupedIssuesList_UsesVirtualizingStackPanel()
    {
        string xaml = ReadControlXaml("Shell", "DdlSchemaAnalysisWorkspaceControl.axaml");

        Assert.Contains("ItemsSource=\"{Binding SchemaAnalysisPanel.GroupedIssues}\"", xaml);
        Assert.Contains("<VirtualizingStackPanel/>", xaml);
    }

    private static string ReadControlXaml(string folder, string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "DBWeaver.UI",
                "Controls",
                folder,
                fileName);

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate {fileName} from test base directory.");
    }
}

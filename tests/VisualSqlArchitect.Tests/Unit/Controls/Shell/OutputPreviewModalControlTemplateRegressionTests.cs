using System.IO;

namespace DBWeaver.Tests.Unit.Controls.Shell;

public class OutputPreviewModalControlTemplateRegressionTests
{
    [Fact]
    public void DdlOutput_UsesAvaloniaEditReadonlyEditor()
    {
        string xaml = ReadControlXaml();

        Assert.Contains("xmlns:ae=\"clr-namespace:AvaloniaEdit;assembly=AvaloniaEdit\"", xaml);
        Assert.Contains("<ae:TextEditor Grid.Row=\"2\"", xaml);
        Assert.Contains("Name=\"DdlSqlEditor\"", xaml);
        Assert.Contains("IsReadOnly=\"True\"", xaml);
        Assert.Contains("ShowLineNumbers=\"True\"", xaml);
    }

    [Fact]
    public void DdlOutput_EditorKeepsProjectVisualStyle()
    {
        string xaml = ReadControlXaml();

        Assert.Contains("Style Selector=\"ae|TextEditor.ddl-output\"", xaml);
        Assert.Contains("Background\" Value=\"{StaticResource Bg0Brush}\"", xaml);
        Assert.Contains("BorderBrush\" Value=\"{StaticResource BorderSubtleBrush}\"", xaml);
        Assert.Contains("FontFamily\" Value=\"{StaticResource MonoFont}\"", xaml);
    }

    [Fact]
    public void DdlOutput_ContainsSchemaAnalysisHeaderAndActions()
    {
        string xaml = ReadControlXaml();

        Assert.Contains("preview.schemaAnalysis.run", xaml);
        Assert.Contains("preview.schemaAnalysis.cancel", xaml);
        Assert.Contains("DdlTool.SchemaAnalysisPanel.ClearFiltersCommand", xaml);
        Assert.Contains("preview.schemaAnalysis.clearFilters", xaml);
    }

    [Fact]
    public void DdlOutput_ContainsSchemaAnalysisIssueDetailsAndCandidatesBindings()
    {
        string xaml = ReadControlXaml();

        Assert.Contains("DdlTool.SchemaAnalysisPanel.VisibleIssues", xaml);
        Assert.Contains("DdlTool.SchemaAnalysisPanel.SelectedIssueEvidence", xaml);
        Assert.Contains("DdlTool.SchemaAnalysisPanel.SelectedIssueDiagnostics", xaml);
        Assert.Contains("DdlTool.SchemaAnalysisPanel.VisibleCandidates", xaml);
        Assert.Contains("DdlTool.SchemaAnalysisPanel.CopySqlCommand", xaml);
        Assert.Contains("DdlTool.SchemaAnalysisPanel.ApplyToCanvasCommand", xaml);
    }

    private static string ReadControlXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "src",
                "DBWeaver.UI",
                "Controls",
                "Shell",
                "OutputPreviewModalControl.axaml"
            );

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate OutputPreviewModalControl.axaml from test base directory.");
    }
}

using VisualSqlArchitect.UI.Services.QueryPreview;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.QueryPreview;

public class QueryCompilationValidationStageTests
{
    [Fact]
    public void Execute_InvokesAllValidationDelegates()
    {
        int aliasCalls = 0;
        int windowCalls = 0;
        int compatCalls = 0;
        int predicateCalls = 0;
        int comparisonCalls = 0;
        int notJsonCalls = 0;
        int paginationCalls = 0;
        int hintsCalls = 0;
        int pivotCalls = 0;

        var stage = new QueryCompilationValidationStage(
            (resultNode, ctes, cteMap, errors) => aliasCalls++,
            errors => windowCalls++,
            errors => compatCalls++,
            (resultNode, errors) => predicateCalls++,
            (resultNode, errors) => comparisonCalls++,
            (resultNode, errors) => notJsonCalls++,
            (resultNode, errors) => paginationCalls++,
            (resultNode, errors) => hintsCalls++,
            (resultNode, errors) => pivotCalls++);

        NodeViewModel resultNode = QueryPreviewTestNodeFactory.Node(NodeType.ResultOutput);
        var errors = new List<string>();

        stage.Execute(new QueryCompilationValidationStageInput(
            resultNode,
            [],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            errors));

        Assert.Equal(1, aliasCalls);
        Assert.Equal(1, windowCalls);
        Assert.Equal(1, compatCalls);
        Assert.Equal(1, predicateCalls);
        Assert.Equal(1, comparisonCalls);
        Assert.Equal(1, notJsonCalls);
        Assert.Equal(1, paginationCalls);
        Assert.Equal(1, hintsCalls);
        Assert.Equal(1, pivotCalls);
    }

    [Fact]
    public void Execute_AccumulatesErrorsFromValidationDelegates()
    {
        var stage = new QueryCompilationValidationStage(
            (resultNode, ctes, cteMap, errors) => errors.Add("alias-error"),
            errors => errors.Add("window-error"),
            errors => errors.Add("compat-error"),
            (resultNode, errors) => errors.Add("predicate-error"),
            (resultNode, errors) => errors.Add("comparison-error"),
            (resultNode, errors) => errors.Add("not-json-error"),
            (resultNode, errors) => errors.Add("pagination-error"),
            (resultNode, errors) => errors.Add("hints-error"),
            (resultNode, errors) => errors.Add("pivot-error"));

        NodeViewModel resultNode = QueryPreviewTestNodeFactory.Node(NodeType.ResultOutput);
        var errors = new List<string> { "seed-error" };

        stage.Execute(new QueryCompilationValidationStageInput(
            resultNode,
            [],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            errors));

        Assert.Contains("seed-error", errors);
        Assert.Contains("alias-error", errors);
        Assert.Contains("window-error", errors);
        Assert.Contains("compat-error", errors);
        Assert.Contains("predicate-error", errors);
        Assert.Contains("comparison-error", errors);
        Assert.Contains("not-json-error", errors);
        Assert.Contains("pagination-error", errors);
        Assert.Contains("hints-error", errors);
        Assert.Contains("pivot-error", errors);
    }

}


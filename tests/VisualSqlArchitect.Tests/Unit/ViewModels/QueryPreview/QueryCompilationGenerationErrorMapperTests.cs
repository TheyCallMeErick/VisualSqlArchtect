using VisualSqlArchitect.UI.Services.QueryPreview;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.QueryPreview;

public class QueryCompilationGenerationErrorMapperTests
{
    [Fact]
    public void Map_WhenCteCycleError_ReturnsDetailedGuidance()
    {
        var mapper = new QueryCompilationGenerationErrorMapper();
        var ex = new InvalidOperationException("Cycle detected between CTE definitions: a -> b -> a");

        List<string> messages = mapper.Map(ex).ToList();

        Assert.Equal(2, messages.Count);
        Assert.Contains("Cycle detected between CTE definitions", messages[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CTE cycle detected", messages[1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Map_WhenRecursiveFlagMissing_ReturnsRecursiveGuidance()
    {
        var mapper = new QueryCompilationGenerationErrorMapper();
        var ex = new InvalidOperationException("cte x references itself but is not marked recursive");

        List<string> messages = mapper.Map(ex).ToList();

        Assert.Equal(2, messages.Count);
        Assert.Contains("not marked recursive", messages[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("recursive' flag enabled", messages[1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Map_WhenUnknownError_ReturnsOriginalMessageOnly()
    {
        var mapper = new QueryCompilationGenerationErrorMapper();
        var ex = new Exception("some generic failure");

        List<string> messages = mapper.Map(ex).ToList();

        Assert.Single(messages);
        Assert.Equal("some generic failure", messages[0]);
    }
}


using AkkornStudio.Ddl.SchemaAnalysis.Application.Rules;

namespace AkkornStudio.Ddl.SchemaAnalysis.Application;

public static class SchemaAnalysisServiceFactory
{
    public static SchemaAnalysisService CreateDefault()
    {
        ISchemaAnalysisRule[] rules =
        [
            new FkCatalogInconsistentRule(),
            new MissingFkRule(),
            new NamingConventionViolationRule(),
            new LowSemanticNameRule(),
            new MissingRequiredCommentRule(),
            new Nf1HintMultiValuedRule(),
            new Nf2HintPartialDependencyRule(),
            new Nf3HintTransitiveDependencyRule(),
        ];

        return new SchemaAnalysisService(rules);
    }
}

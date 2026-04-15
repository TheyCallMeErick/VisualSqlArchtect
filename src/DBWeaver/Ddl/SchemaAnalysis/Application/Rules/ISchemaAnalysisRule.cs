using DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

namespace DBWeaver.Ddl.SchemaAnalysis.Application.Rules;

public interface ISchemaAnalysisRule
{
    SchemaRuleCode RuleCode { get; }

    Task<SchemaRuleExecutionResult> ExecuteAsync(
        SchemaAnalysisExecutionContext context,
        CancellationToken cancellationToken = default
    );
}

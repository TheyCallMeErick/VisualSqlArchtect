namespace DBWeaver.Ddl.SchemaAnalysis.Domain.Contracts;

public sealed record SchemaRuleSetting(bool Enabled, double MinConfidence, int MaxIssues);

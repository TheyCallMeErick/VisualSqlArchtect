namespace AkkornStudio.Ddl.SchemaAnalysis.Domain.Contracts;

public sealed record SchemaRuleSetting(bool Enabled, double MinConfidence, int MaxIssues);

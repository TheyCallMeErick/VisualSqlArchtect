namespace DBWeaver.Ddl.SchemaAnalysis.Domain.Enums;

public enum RuleExecutionState
{
    NotStarted,
    Running,
    Completed,
    Skipped,
    Failed,
    TimedOut,
    Cancelled,
}

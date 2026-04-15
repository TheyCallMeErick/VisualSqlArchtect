namespace AkkornStudio.Ddl.SchemaAnalysis.Domain.Validation;

public sealed record SchemaContractValidationError(string Code, string Message, string Path);

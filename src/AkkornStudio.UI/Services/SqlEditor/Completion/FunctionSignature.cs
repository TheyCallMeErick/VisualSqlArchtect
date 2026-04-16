namespace AkkornStudio.UI.Services.SqlEditor;

public sealed record FunctionSignature(
    string Name,
    IReadOnlyList<FunctionParameterSignature> Parameters,
    string ReturnType);

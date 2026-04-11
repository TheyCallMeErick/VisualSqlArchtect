namespace DBWeaver.UI.Services.SqlEditor;

public sealed record SignatureHelpInfo(
    FunctionSignature Signature,
    int ActiveParameterIndex,
    string DisplayText);

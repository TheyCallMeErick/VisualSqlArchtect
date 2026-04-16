namespace AkkornStudio.UI.Services.SqlEditor;

public sealed record SqlInlineEditEligibility(
    bool IsEligible,
    string? TableFullName,
    IReadOnlyList<string> PrimaryKeyColumns,
    IReadOnlyList<string> EditableColumns)
{
    public static SqlInlineEditEligibility NotEligible { get; } =
        new(false, null, [], []);
}

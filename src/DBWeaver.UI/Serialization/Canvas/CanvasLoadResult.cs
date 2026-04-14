using DBWeaver.UI.Services.Workspace.Models;

namespace DBWeaver.UI.Serialization;

/// <summary>
/// Describes the outcome of deserialising a canvas file.
/// </summary>
public sealed record CanvasLoadResult(
    bool Success,
    string? Error = null,
    IReadOnlyList<string>? Warnings = null,
    WorkspaceDocumentType? ActiveDocumentType = null,
    IReadOnlyList<string>? SqlEditorSeedScripts = null
)
{
    public static CanvasLoadResult Ok(
        IReadOnlyList<string>? warnings = null,
        WorkspaceDocumentType? activeDocumentType = null,
        IReadOnlyList<string>? sqlEditorSeedScripts = null) =>
        new(true, null, warnings, activeDocumentType, sqlEditorSeedScripts);

    public static CanvasLoadResult Fail(string error) => new(false, error, null);
}

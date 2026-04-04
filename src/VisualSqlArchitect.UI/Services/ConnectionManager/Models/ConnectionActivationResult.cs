using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata;

namespace VisualSqlArchitect.UI.Services.ConnectionManager;

public readonly record struct ConnectionActivationResult(
    EConnectionActivationOutcome Outcome,
    ConnectionConfig? Config = null,
    DbMetadata? Metadata = null,
    bool ShouldOpenClearCanvasPrompt = false,
    string? FailureReason = null,
    Exception? FailureException = null);


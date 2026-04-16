using System.Text.Json;

namespace AkkornStudio.UI.Services.Workspace.Models;

public sealed record WorkspaceDocumentDescriptor(
    Guid DocumentId,
    WorkspaceDocumentType DocumentType,
    string Title,
    bool IsDirty,
    string PersistenceSchemaVersion,
    JsonElement Payload);

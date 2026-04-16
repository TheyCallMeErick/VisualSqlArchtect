using AkkornStudio.UI.Services.Workspace.Models;

namespace AkkornStudio.UI.Services.Workspace.Diagnostics;

public interface IWorkspaceDocumentDiagnosticsContractRegistry
{
    WorkspaceDocumentDiagnosticsContract Resolve(WorkspaceDocumentType documentType);
}

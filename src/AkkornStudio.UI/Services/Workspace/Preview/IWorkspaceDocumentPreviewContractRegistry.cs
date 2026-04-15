using AkkornStudio.UI.Services.Workspace.Models;

namespace AkkornStudio.UI.Services.Workspace.Preview;

public interface IWorkspaceDocumentPreviewContractRegistry
{
    WorkspaceDocumentPreviewContract Resolve(WorkspaceDocumentType documentType);
}

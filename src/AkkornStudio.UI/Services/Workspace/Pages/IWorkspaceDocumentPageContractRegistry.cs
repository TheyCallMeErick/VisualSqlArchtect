using AkkornStudio.UI.Services.Workspace.Models;

namespace AkkornStudio.UI.Services.Workspace.Pages;

public interface IWorkspaceDocumentPageContractRegistry
{
    WorkspaceDocumentPageContract Resolve(WorkspaceDocumentType documentType);
}

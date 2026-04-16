using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.UI.Services.SqlEditor;

public interface ISqlEditorViewModelFactory
{
    SqlEditorViewModel Create(SqlEditorViewModelFactoryContext context);
}


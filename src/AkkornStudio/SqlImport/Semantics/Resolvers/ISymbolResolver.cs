using AkkornStudio.SqlImport.Contracts;

namespace AkkornStudio.SqlImport.Semantics.Resolvers;

public interface ISymbolResolver
{
    SymbolResolutionResult ResolveColumn(
        string scopeId,
        string? qualifier,
        string column
    );
}

using DBWeaver.SqlImport.Contracts;

namespace DBWeaver.SqlImport.Semantics.Resolvers;

public interface ISymbolResolver
{
    SymbolResolutionResult ResolveColumn(
        string scopeId,
        string? qualifier,
        string column
    );
}

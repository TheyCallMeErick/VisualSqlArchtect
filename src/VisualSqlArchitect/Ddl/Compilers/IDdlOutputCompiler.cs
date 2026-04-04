using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.Ddl.Compilers;

internal interface IDdlOutputCompiler
{
    NodeType OutputType { get; }

    void Compile(
        IReadOnlyList<NodeInstance> outputNodes,
        DdlOutputCompilationContext context,
        ICollection<IDdlExpression> statements
    );
}

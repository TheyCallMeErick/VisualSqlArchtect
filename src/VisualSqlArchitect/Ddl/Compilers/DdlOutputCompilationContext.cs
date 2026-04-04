using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.Ddl.Compilers;

internal sealed class DdlOutputCompilationContext
{
    public DdlOutputCompilationContext(
        NodeGraph graph,
        Func<NodeInstance, DdlIdempotentMode> readIdempotentMode,
        Func<NodeInstance, DdlIdempotentMode, CreateEnumTypeExpr> compileEnumTypeDefinition,
        Func<NodeInstance, DdlIdempotentMode, CreateSequenceExpr> compileSequenceDefinition,
        Func<NodeInstance, DdlIdempotentMode, CreateTableExpr> compileTableDefinition,
        Func<NodeInstance, CreateTableAsExpr> compileCreateTableAsOutput,
        Func<NodeInstance, DdlIdempotentMode, CreateViewExpr> compileCreateViewDefinition,
        Func<NodeInstance, AlterViewExpr> compileAlterViewDefinition,
        Func<NodeInstance, CreateIndexExpr> compileIndexDefinition,
        Func<NodeInstance, NodeInstance, AlterTableExpr> compileAlterTableOutput,
        Action<string, string, string?> addError
    )
    {
        Graph = graph ?? throw new ArgumentNullException(nameof(graph));
        ReadIdempotentMode = readIdempotentMode ?? throw new ArgumentNullException(nameof(readIdempotentMode));
        CompileEnumTypeDefinition = compileEnumTypeDefinition ?? throw new ArgumentNullException(nameof(compileEnumTypeDefinition));
        CompileSequenceDefinition = compileSequenceDefinition ?? throw new ArgumentNullException(nameof(compileSequenceDefinition));
        CompileTableDefinition = compileTableDefinition ?? throw new ArgumentNullException(nameof(compileTableDefinition));
        CompileCreateTableAsOutput = compileCreateTableAsOutput ?? throw new ArgumentNullException(nameof(compileCreateTableAsOutput));
        CompileCreateViewDefinition = compileCreateViewDefinition ?? throw new ArgumentNullException(nameof(compileCreateViewDefinition));
        CompileAlterViewDefinition = compileAlterViewDefinition ?? throw new ArgumentNullException(nameof(compileAlterViewDefinition));
        CompileIndexDefinition = compileIndexDefinition ?? throw new ArgumentNullException(nameof(compileIndexDefinition));
        CompileAlterTableOutput = compileAlterTableOutput ?? throw new ArgumentNullException(nameof(compileAlterTableOutput));
        AddError = addError ?? throw new ArgumentNullException(nameof(addError));
    }

    public NodeGraph Graph { get; }
    public Func<NodeInstance, DdlIdempotentMode> ReadIdempotentMode { get; }
    public Func<NodeInstance, DdlIdempotentMode, CreateEnumTypeExpr> CompileEnumTypeDefinition { get; }
    public Func<NodeInstance, DdlIdempotentMode, CreateSequenceExpr> CompileSequenceDefinition { get; }
    public Func<NodeInstance, DdlIdempotentMode, CreateTableExpr> CompileTableDefinition { get; }
    public Func<NodeInstance, CreateTableAsExpr> CompileCreateTableAsOutput { get; }
    public Func<NodeInstance, DdlIdempotentMode, CreateViewExpr> CompileCreateViewDefinition { get; }
    public Func<NodeInstance, AlterViewExpr> CompileAlterViewDefinition { get; }
    public Func<NodeInstance, CreateIndexExpr> CompileIndexDefinition { get; }
    public Func<NodeInstance, NodeInstance, AlterTableExpr> CompileAlterTableOutput { get; }
    public Action<string, string, string?> AddError { get; }
}

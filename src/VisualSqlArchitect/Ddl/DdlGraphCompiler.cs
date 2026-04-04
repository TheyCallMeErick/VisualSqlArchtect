using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Ddl.Compilers;
using VisualSqlArchitect.QueryEngine;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VisualSqlArchitect.Ddl;

/// <summary>
/// Compiles a DDL-oriented NodeGraph into DDL expression trees.
/// Phase 4 scope: CreateTableOutput only.
/// </summary>
public sealed class DdlGraphCompiler(NodeGraph graph, DatabaseProvider provider = DatabaseProvider.SqlServer)
{
    private readonly NodeGraph _graph = graph ?? throw new ArgumentNullException(nameof(graph));
    private readonly DatabaseProvider _provider = provider;
    private readonly List<DdlCompileDiagnostic> _diagnostics = [];

    public IReadOnlyList<IDdlExpression> Compile()
        => CompileWithDiagnostics().Statements;

    public DdlCompileResult CompileWithDiagnostics()
    {
        _diagnostics.Clear();

        var createTableOutputs = _graph.Nodes.Where(n => n.Type == NodeType.CreateTableOutput).ToList();
        var createTypeOutputs = _graph.Nodes.Where(n => n.Type == NodeType.CreateTypeOutput).ToList();
        var createSequenceOutputs = _graph.Nodes.Where(n => n.Type == NodeType.CreateSequenceOutput).ToList();
        var createTableAsOutputs = _graph.Nodes.Where(n => n.Type == NodeType.CreateTableAsOutput).ToList();
        var createViewOutputs = _graph.Nodes.Where(n => n.Type == NodeType.CreateViewOutput).ToList();
        var alterViewOutputs = _graph.Nodes.Where(n => n.Type == NodeType.AlterViewOutput).ToList();
        var createIndexOutputs = _graph.Nodes.Where(n => n.Type == NodeType.CreateIndexOutput).ToList();
        var alterTableOutputs = _graph.Nodes.Where(n => n.Type == NodeType.AlterTableOutput).ToList();

        ValidateStaticRules(createTableOutputs, createTypeOutputs, createSequenceOutputs, createTableAsOutputs, createViewOutputs, alterViewOutputs);

        if (createTableOutputs.Count == 0 && createTypeOutputs.Count == 0 && createSequenceOutputs.Count == 0 && createTableAsOutputs.Count == 0 && createViewOutputs.Count == 0 && alterViewOutputs.Count == 0 && createIndexOutputs.Count == 0 && alterTableOutputs.Count == 0)
            return new DdlCompileResult([], [.. _diagnostics]);

        if (_diagnostics.Any(d => d.Severity == DdlDiagnosticSeverity.Error))
            return new DdlCompileResult([], [.. _diagnostics]);

        var statements = new List<IDdlExpression>();
        var createSequenceOutputCompiler = new CreateSequenceOutputCompiler();
        createSequenceOutputCompiler.Compile(
            createSequenceOutputs,
            CreateOutputCompilationContext(),
            statements
        );

        var createTypeOutputCompiler = new CreateTypeOutputCompiler();
        createTypeOutputCompiler.Compile(
            createTypeOutputs,
            CreateOutputCompilationContext(),
            statements
        );

        var createTableOutputCompiler = new CreateTableOutputCompiler();
        createTableOutputCompiler.Compile(
            createTableOutputs,
            CreateOutputCompilationContext(),
            statements
        );

        var createTableAsOutputCompiler = new CreateTableAsOutputCompiler();
        createTableAsOutputCompiler.Compile(
            createTableAsOutputs,
            CreateOutputCompilationContext(),
            statements
        );

        var createViewOutputCompiler = new CreateViewOutputCompiler();
        createViewOutputCompiler.Compile(
            createViewOutputs,
            CreateOutputCompilationContext(),
            statements
        );

        var alterViewOutputCompiler = new AlterViewOutputCompiler();
        alterViewOutputCompiler.Compile(
            alterViewOutputs,
            CreateOutputCompilationContext(),
            statements
        );

        var createIndexOutputCompiler = new CreateIndexOutputCompiler();
        createIndexOutputCompiler.Compile(
            createIndexOutputs,
            CreateOutputCompilationContext(),
            statements
        );

        var alterTableOutputCompiler = new AlterTableOutputCompiler();
        alterTableOutputCompiler.Compile(
            alterTableOutputs,
            CreateOutputCompilationContext(),
            statements
        );

        return new DdlCompileResult(statements, [.. _diagnostics]);
    }

    private CreateTableExpr CompileTableDefinition(NodeInstance tableNode, DdlIdempotentMode idempotentMode)
    {
        string schemaName = ReadParam(tableNode, "SchemaName", "dbo");
        string tableName = ReadParam(tableNode, "TableName", "");
        bool ifNotExists = ReadBoolParam(tableNode, "IfNotExists", false);
        string tableComment = ReadParam(tableNode, "Comment", "");

        if (string.IsNullOrWhiteSpace(tableName))
            throw new InvalidOperationException("TableDefinition requires TableName.");

        var columnWires = _graph.Connections
            .Where(c => c.ToNodeId == tableNode.Id && c.ToPinName == "column")
            .ToList();

        var columnsByNodeId = new Dictionary<string, DdlColumnExpr>(StringComparer.Ordinal);
        foreach (Connection wire in columnWires)
        {
            NodeInstance columnNode = _graph.NodeMap[wire.FromNodeId];
            if (columnNode.Type != NodeType.ColumnDefinition)
                continue;

            columnsByNodeId[columnNode.Id] = BuildColumnFromDefinitionNode(columnNode);
        }

        // Default constraints are translated into inline column DEFAULT for phase 4.
        ApplyDefaultConstraints(columnsByNodeId, tableNode.Id);

        var constraintNodes = _graph.Connections
            .Where(c => c.ToNodeId == tableNode.Id && c.ToPinName == "constraint")
            .Select(c => _graph.NodeMap[c.FromNodeId])
            .DistinctBy(n => n.Id)
            .ToList();

        var pks = new List<DdlPrimaryKeyExpr>();
        var uqs = new List<DdlUniqueExpr>();
        var checks = new List<DdlCheckExpr>();

        foreach (NodeInstance constraintNode in constraintNodes)
        {
            switch (constraintNode.Type)
            {
                case NodeType.PrimaryKeyConstraint:
                    {
                        IReadOnlyList<string> cols = ResolveConstraintColumnNames(constraintNode.Id, "column", columnsByNodeId);
                        if (cols.Count > 0)
                            pks.Add(new DdlPrimaryKeyExpr(ReadParam(constraintNode, "ConstraintName", ""), cols));
                        break;
                    }
                case NodeType.UniqueConstraint:
                    {
                        IReadOnlyList<string> cols = ResolveConstraintColumnNames(constraintNode.Id, "column", columnsByNodeId);
                        if (cols.Count > 0)
                            uqs.Add(new DdlUniqueExpr(ReadParam(constraintNode, "ConstraintName", ""), cols));
                        break;
                    }
                case NodeType.CheckConstraint:
                    {
                        string expression = ReadParam(constraintNode, "Expression", "");
                        if (!string.IsNullOrWhiteSpace(expression))
                            checks.Add(new DdlCheckExpr(ReadParam(constraintNode, "ConstraintName", ""), expression));
                        break;
                    }
                case NodeType.ForeignKeyConstraint:
                    {
                        AddWarning(
                            "W-DDL-FOREIGNKEY-PHASE",
                            "ForeignKeyConstraint is not emitted in the current DDL phase and will be ignored.",
                            constraintNode.Id
                        );
                        break;
                    }
            }
        }

        return new CreateTableExpr(
            schemaName,
            tableName,
            ifNotExists,
            [.. columnsByNodeId.Values],
            pks,
            uqs,
            checks,
            tableComment,
            idempotentMode
        );
    }

    private CreateIndexExpr CompileIndexDefinition(NodeInstance indexNode)
    {
        Connection? tableWire = _graph.GetSingleInputConnection(indexNode.Id, "table");
        if (tableWire is null)
            throw new InvalidOperationException("IndexDefinition requires a connected 'table' input.");

        NodeInstance tableNode = _graph.NodeMap[tableWire.FromNodeId];
        if (tableNode.Type != NodeType.TableDefinition)
            throw new InvalidOperationException("IndexDefinition.table must come from TableDefinition.");

        string schemaName = ReadParam(tableNode, "SchemaName", "dbo");
        string tableName = ReadParam(tableNode, "TableName", "");
        if (string.IsNullOrWhiteSpace(tableName))
            throw new InvalidOperationException("TableDefinition requires TableName for index generation.");

        string indexName = ReadParam(indexNode, "IndexName", "");
        if (string.IsNullOrWhiteSpace(indexName))
            throw new InvalidOperationException("IndexDefinition requires IndexName.");

        bool isUnique = ReadBoolParam(indexNode, "IsUnique", false);
        bool ifNotExists = true;

        var allColumnsByNodeId = BuildColumnsByNodeIdForTable(tableNode.Id);
        IReadOnlyList<string> keyColumns = ResolveConstraintColumnNames(indexNode.Id, "column", allColumnsByNodeId);
        IReadOnlyList<string> expressionColumns = ResolveExpressionColumns(indexNode);

        if (expressionColumns.Count > 0 && _provider == DatabaseProvider.SqlServer)
        {
            AddWarning(
                "W-DDL-INDEX-EXPR-UNSUPPORTED-SQLSERVER",
                "SQL Server nao suporta indice em expressao arbitraria; use coluna computada persistida.",
                indexNode.Id
            );
            expressionColumns = [];
        }

        if (expressionColumns.Count > 0 && _provider == DatabaseProvider.SQLite)
        {
            AddWarning(
                "W-DDL-INDEX-EXPR-SQLITE-PARTIAL",
                "SQLite suporta indice em expressao com limitacoes; valide a expressao no provider alvo.",
                indexNode.Id
            );
        }

        if (keyColumns.Count == 0 && expressionColumns.Count == 0)
            throw new InvalidOperationException("IndexDefinition requires at least one key column or expression column.");

        var keyEntries = new List<DdlIndexKeyExpr>();
        keyEntries.AddRange(keyColumns.Select(c => new DdlIndexKeyExpr(ColumnName: c)));
        keyEntries.AddRange(expressionColumns.Select(e => new DdlIndexKeyExpr(ExpressionSql: e)));

        IReadOnlyList<string> includeColumns = ResolveConstraintColumnNames(indexNode.Id, "include_column", allColumnsByNodeId);
        if (includeColumns.Count > 0 && _provider is not DatabaseProvider.SqlServer and not DatabaseProvider.Postgres)
        {
            AddWarning(
                "W-DDL-INDEX-INCLUDE-UNSUPPORTED",
                $"Provider {_provider} does not support INCLUDE columns in CREATE INDEX; include columns will be ignored.",
                indexNode.Id
            );
        }

        return new CreateIndexExpr(
            schemaName,
            tableName,
            indexName,
            isUnique,
            keyEntries,
            includeColumns,
            ifNotExists
        );
    }

    private CreateTableAsExpr CompileCreateTableAsOutput(NodeInstance outputNode)
    {
        DdlIdempotentMode mode = ReadIdempotentMode(outputNode);
        string schema = ReadParam(outputNode, "Schema", _provider == DatabaseProvider.SqlServer ? "dbo" : "public");
        string tableName = ReadParam(outputNode, "TableName", "");
        if (string.IsNullOrWhiteSpace(tableName))
            throw new InvalidOperationException("CreateTableAsOutput requires TableName.");

        bool includeData = ReadBoolParam(outputNode, "IncludeData", true);
        Connection? queryConn = _graph.GetSingleInputConnection(outputNode.Id, "source_query");
        Connection? tableConn = _graph.GetSingleInputConnection(outputNode.Id, "source_table");
        string fallbackSelectSql = ReadParam(outputNode, "SelectSql", "");

        if (queryConn is not null)
            throw new InvalidOperationException("CreateTableAsOutput.source_query nao e permitido no canvas DDL isolado.");

        if (tableConn is null && string.IsNullOrWhiteSpace(fallbackSelectSql))
            throw new InvalidOperationException("CreateTableAsOutput requires source_table or SelectSql.");

        if (_provider != DatabaseProvider.Postgres && !includeData)
        {
            AddWarning(
                "W-DDL-CREATETABLEAS-INCLUDEDATA-IGNORED",
                "IncludeData afeta apenas PostgreSQL; sera ignorado neste provider.",
                outputNode.Id
            );
        }

        if (mode == DdlIdempotentMode.IfNotExists)
        {
            AddWarning(
                "W-DDL-CREATETABLEAS-IFNOTEXISTS-UNSUPPORTED",
                "IfNotExists nao e suportado de forma portavel para CREATE TABLE AS; modo sera degradado.",
                outputNode.Id
            );
        }

        if (tableConn is null)
            return new CreateTableAsExpr(schema, tableName, null, fallbackSelectSql, includeData, mode);

        NodeInstance tableNode = _graph.NodeMap[tableConn.FromNodeId];
        if (tableNode.Type != NodeType.TableDefinition)
            throw new InvalidOperationException("CreateTableAsOutput.source_table must come from TableDefinition.");

        string srcSchema = ReadParam(tableNode, "SchemaName", _provider == DatabaseProvider.SqlServer ? "dbo" : "public");
        string srcTable = ReadParam(tableNode, "TableName", "");
        if (string.IsNullOrWhiteSpace(srcTable))
            throw new InvalidOperationException("source_table TableDefinition requires TableName.");

        string sourceQualified = BuildQualifiedName(srcSchema, srcTable);
        return new CreateTableAsExpr(schema, tableName, sourceQualified, null, includeData, mode);
    }

    private string BuildQualifiedName(string schema, string name)
    {
        string Quote(string value) => _provider switch
        {
            DatabaseProvider.SqlServer => $"[{value.Replace("]", "]]" )}]",
            DatabaseProvider.MySql => $"`{value.Replace("`", "``")}`",
            DatabaseProvider.Postgres or DatabaseProvider.SQLite => $"\"{value.Replace("\"", "\"\"")}\"",
            _ => value,
        };

        return $"{Quote(schema)}.{Quote(name)}";
    }

    private IReadOnlyList<string> ResolveExpressionColumns(NodeInstance indexNode)
    {
        var expressions = new List<string>();

        if (indexNode.PinLiterals.TryGetValue("expression_column", out string? literal)
            && !string.IsNullOrWhiteSpace(literal))
        {
            expressions.Add(literal.Trim());
        }

        foreach (Connection wire in _graph.Connections.Where(c => c.ToNodeId == indexNode.Id && c.ToPinName == "expression_column"))
        {
            NodeInstance fromNode = _graph.NodeMap[wire.FromNodeId];

            string expr = fromNode.Type switch
            {
                NodeType.ValueString => ReadParam(fromNode, "value", ""),
                NodeType.CheckConstraint => ReadParam(fromNode, "Expression", ""),
                _ => string.Empty,
            };

            if (!string.IsNullOrWhiteSpace(expr))
                expressions.Add(expr.Trim());
        }

        return expressions
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private AlterTableExpr CompileAlterTableOutput(NodeInstance outputNode, NodeInstance tableNode)
    {
        string schemaName = ReadParam(tableNode, "SchemaName", "dbo");
        string tableName = ReadParam(tableNode, "TableName", "");
        if (string.IsNullOrWhiteSpace(tableName))
            throw new InvalidOperationException("TableDefinition requires TableName for alter operations.");

        bool emitSeparateStatements = ReadBoolParam(outputNode, "EmitSeparateStatements", true);

        IReadOnlyList<Connection> operationWires =
        [
            .. _graph.Connections.Where(c => c.ToNodeId == outputNode.Id && c.ToPinName == "operation"),
        ];

        if (operationWires.Count == 0)
            throw new InvalidOperationException("AlterTableOutput requires at least one connected operation.");

        var operations = new List<IAlterOpExpr>();
        foreach (Connection wire in operationWires)
        {
            NodeInstance opNode = _graph.NodeMap[wire.FromNodeId];
            IAlterOpExpr opExpr = CompileAlterOperation(opNode);
            operations.Add(opExpr);
        }

        // Safety ordering: destructive operations execute first when mixed.
        operations = operations
            .OrderByDescending(op => op.IsDestructive)
            .ToList();

        return new AlterTableExpr(schemaName, tableName, operations, emitSeparateStatements);
    }

    private IAlterOpExpr CompileAlterOperation(NodeInstance opNode)
    {
        switch (opNode.Type)
        {
            case NodeType.AddColumnOp:
                {
                    Connection? columnWire = _graph.GetSingleInputConnection(opNode.Id, "column");
                    if (columnWire is null)
                        throw new InvalidOperationException("AddColumnOp requires a connected 'column' input.");

                    NodeInstance columnNode = _graph.NodeMap[columnWire.FromNodeId];
                    if (columnNode.Type != NodeType.ColumnDefinition)
                        throw new InvalidOperationException("AddColumnOp.column must come from ColumnDefinition.");

                    DdlColumnExpr colExpr = BuildColumnFromDefinitionNode(columnNode);
                    return new AddColumnOpExpr(colExpr);
                }
            case NodeType.DropColumnOp:
                {
                    string columnName = ReadParam(opNode, "ColumnName", "");
                    if (string.IsNullOrWhiteSpace(columnName))
                        throw new InvalidOperationException("DropColumnOp requires ColumnName.");
                    bool ifExists = ReadBoolParam(opNode, "IfExists", false);
                    return new DropColumnOpExpr(columnName, ifExists);
                }
            case NodeType.RenameColumnOp:
                {
                    string oldName = ReadParam(opNode, "OldName", "");
                    string newName = ReadParam(opNode, "NewName", "");
                    if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
                        throw new InvalidOperationException("RenameColumnOp requires OldName and NewName.");
                    return new RenameColumnOpExpr(oldName, newName);
                }
            case NodeType.RenameTableOp:
                {
                    string newName = ReadParam(opNode, "NewName", "");
                    string? newSchema = ReadParam(opNode, "NewSchema", "");
                    if (string.IsNullOrWhiteSpace(newName))
                        throw new InvalidOperationException("RenameTableOp requires NewName.");

                    return new RenameTableOpExpr(newName, string.IsNullOrWhiteSpace(newSchema) ? null : newSchema);
                }
            case NodeType.DropTableOp:
                {
                    bool ifExists = ReadBoolParam(opNode, "IfExists", false);
                    return new DropTableOpExpr(ifExists);
                }
            case NodeType.AlterColumnTypeOp:
                {
                    Connection? newColumnWire = _graph.GetSingleInputConnection(opNode.Id, "new_column");
                    if (newColumnWire is null)
                        throw new InvalidOperationException("AlterColumnTypeOp requires a connected 'new_column' input.");

                    NodeInstance columnNode = _graph.NodeMap[newColumnWire.FromNodeId];
                    if (columnNode.Type != NodeType.ColumnDefinition)
                        throw new InvalidOperationException("AlterColumnTypeOp.new_column must come from ColumnDefinition.");

                    DdlColumnExpr colExpr = BuildColumnFromDefinitionNode(columnNode);

                    return new AlterColumnTypeOpExpr(colExpr.ColumnName, colExpr.DataType, colExpr.IsNullable);
                }
            default:
                throw new InvalidOperationException($"Unsupported alter operation node type: {opNode.Type}.");
        }
    }

    private DdlColumnExpr BuildColumnFromDefinitionNode(NodeInstance columnNode)
    {
        string columnName = ReadParam(columnNode, "ColumnName", "");
        if (string.IsNullOrWhiteSpace(columnName))
            throw new InvalidOperationException("ColumnDefinition requires ColumnName.");

        string dataType;
        bool useNativeType = ReadBoolParam(columnNode, "UseNativeType", false);
        if (useNativeType)
        {
            string nativeTypeExpression = ReadParam(columnNode, "NativeTypeExpression", "");
            if (string.IsNullOrWhiteSpace(nativeTypeExpression))
                throw new InvalidOperationException("ColumnDefinition with UseNativeType=true requires NativeTypeExpression.");

            dataType = nativeTypeExpression;
            AddWarning(
                "W-DDL-NATIVE-TYPE-PORTABILITY",
                "NativeTypeExpression pode reduzir portabilidade entre providers.",
                columnNode.Id
            );
        }
        else
        {
            Connection? typeWire = _graph.GetSingleInputConnection(columnNode.Id, "type_def");
            if (typeWire is not null)
            {
                NodeInstance typeNode = _graph.NodeMap[typeWire.FromNodeId];
                dataType = typeNode.Type switch
                {
                    NodeType.EnumTypeDefinition => ResolveEnumColumnDataType(BuildEnumTypeSpec(typeNode)),
                    NodeType.ScalarTypeDefinition => ResolveScalarColumnDataType(BuildScalarTypeSpec(typeNode)),
                    _ => throw new InvalidOperationException("ColumnDefinition.type_def must come from EnumTypeDefinition or ScalarTypeDefinition."),
                };
            }
            else
            {
                dataType = ReadParam(columnNode, "DataType", "INT");
            }
        }

        bool isNullable = ReadBoolParam(columnNode, "IsNullable", false);
        string? sequenceDefaultExpression = null;
        Connection? sequenceWire = _graph.GetSingleInputConnection(columnNode.Id, "sequence");
        if (sequenceWire is not null)
        {
            NodeInstance sequenceNode = _graph.NodeMap[sequenceWire.FromNodeId];
            if (sequenceNode.Type != NodeType.SequenceDefinition)
                throw new InvalidOperationException("ColumnDefinition.sequence must come from SequenceDefinition.");

            SequenceSpec sequenceSpec = BuildSequenceSpec(sequenceNode);
            sequenceDefaultExpression = ResolveSequenceDefaultExpression(sequenceSpec);
            if (string.IsNullOrWhiteSpace(sequenceDefaultExpression))
            {
                AddWarning(
                    "W-DDL-SEQUENCE-DEFAULT-UNSUPPORTED",
                    $"Provider {_provider} não suporta DEFAULT baseado em sequence nativa.",
                    columnNode.Id
                );
            }
        }

        string comment = ReadParam(columnNode, "Comment", "");
        return new DdlColumnExpr(columnName, dataType, isNullable, sequenceDefaultExpression, comment);
    }

    private Dictionary<string, DdlColumnExpr> BuildColumnsByNodeIdForTable(string tableNodeId)
    {
        var columnWires = _graph.Connections
            .Where(c => c.ToNodeId == tableNodeId && c.ToPinName == "column")
            .ToList();

        var columnsByNodeId = new Dictionary<string, DdlColumnExpr>(StringComparer.Ordinal);
        foreach (Connection wire in columnWires)
        {
            NodeInstance columnNode = _graph.NodeMap[wire.FromNodeId];
            if (columnNode.Type != NodeType.ColumnDefinition)
                continue;

            string columnName = ReadParam(columnNode, "ColumnName", "");
            if (string.IsNullOrWhiteSpace(columnName))
                continue;

            columnsByNodeId[columnNode.Id] = BuildColumnFromDefinitionNode(columnNode);
        }

        return columnsByNodeId;
    }

    private IReadOnlyList<string> ResolveConstraintColumnNames(
        string constraintNodeId,
        string inputPinName,
        IReadOnlyDictionary<string, DdlColumnExpr> columnsByNodeId
    )
    {
        return _graph.Connections
            .Where(c => c.ToNodeId == constraintNodeId && c.ToPinName == inputPinName)
            .Select(c => columnsByNodeId.TryGetValue(c.FromNodeId, out DdlColumnExpr? col) ? col.ColumnName : null)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ApplyDefaultConstraints(Dictionary<string, DdlColumnExpr> columnsByNodeId, string tableNodeId)
    {
        var defaultConstraintNodes = _graph.Connections
            .Where(c => c.ToNodeId == tableNodeId && c.ToPinName == "constraint")
            .Select(c => _graph.NodeMap[c.FromNodeId])
            .Where(n => n.Type == NodeType.DefaultConstraint)
            .DistinctBy(n => n.Id)
            .ToList();

        foreach (NodeInstance defaultNode in defaultConstraintNodes)
        {
            Connection? columnWire = _graph.GetSingleInputConnection(defaultNode.Id, "column");
            if (columnWire is null || !columnsByNodeId.TryGetValue(columnWire.FromNodeId, out DdlColumnExpr? columnExpr))
                continue;

            string defaultExpr = ReadParam(defaultNode, "DefaultValue", "");
            if (string.IsNullOrWhiteSpace(defaultExpr))
                continue;

            columnsByNodeId[columnWire.FromNodeId] = columnExpr with { DefaultExpression = defaultExpr };
        }
    }

    private CreateEnumTypeExpr CompileEnumTypeDefinition(NodeInstance typeNode, DdlIdempotentMode idempotentMode)
    {
        if (_provider != DatabaseProvider.Postgres)
            throw new InvalidOperationException("CreateTypeOutput é suportado apenas para PostgreSQL.");

        EnumTypeSpec spec = BuildEnumTypeSpec(typeNode);
        return new CreateEnumTypeExpr(spec.SchemaName, spec.TypeName, spec.Values, idempotentMode);
    }

    private CreateSequenceExpr CompileSequenceDefinition(NodeInstance sequenceNode, DdlIdempotentMode idempotentMode)
    {
        SequenceSpec spec = BuildSequenceSpec(sequenceNode);
        return new CreateSequenceExpr(
            spec.Schema,
            spec.SequenceName,
            spec.StartValue,
            spec.Increment,
            spec.MinValue,
            spec.MaxValue,
            spec.Cycle,
            spec.Cache,
            idempotentMode
        );
    }

    private CreateViewExpr CompileCreateViewDefinition(NodeInstance viewNode, DdlIdempotentMode idempotentMode)
    {
        string schema = ReadParam(viewNode, "Schema", "public");
        string viewName = ReadParam(viewNode, "ViewName", "");
        bool orReplace = ReadBoolParam(viewNode, "OrReplace", false);
        bool isMaterialized = ReadBoolParam(viewNode, "IsMaterialized", false);
        string selectSql = ResolveViewSelectSql(viewNode);

        if (_provider != DatabaseProvider.Postgres && isMaterialized)
            throw new InvalidOperationException("Materialized view é suportada apenas no PostgreSQL.");

        if (_provider == DatabaseProvider.Postgres && isMaterialized && orReplace)
            throw new InvalidOperationException("PostgreSQL não suporta CREATE OR REPLACE MATERIALIZED VIEW.");

        if (string.IsNullOrWhiteSpace(viewName))
            throw new InvalidOperationException("ViewDefinition requires ViewName.");

        return new CreateViewExpr(schema, viewName, orReplace, isMaterialized, selectSql, idempotentMode);
    }

    private AlterViewExpr CompileAlterViewDefinition(NodeInstance viewNode)
    {
        string schema = ReadParam(viewNode, "Schema", "public");
        string viewName = ReadParam(viewNode, "ViewName", "");
        string selectSql = ResolveViewSelectSql(viewNode);

        if (string.IsNullOrWhiteSpace(viewName))
            throw new InvalidOperationException("ViewDefinition requires ViewName.");

        return new AlterViewExpr(schema, viewName, selectSql);
    }

    private EnumTypeSpec BuildEnumTypeSpec(NodeInstance typeNode)
    {
        string typeName = ReadParam(typeNode, "TypeName", "");
        if (string.IsNullOrWhiteSpace(typeName))
            throw new InvalidOperationException("EnumTypeDefinition requires TypeName.");

        string schemaName = ReadParam(typeNode, "SchemaName", "public");
        string rawValues = ReadParam(typeNode, "EnumValues", "");
        IReadOnlyList<string> values = ParseEnumValues(rawValues);

        if (values.Count == 0)
            throw new InvalidOperationException("EnumTypeDefinition requires at least one value.");

        return new EnumTypeSpec(schemaName, typeName, values);
    }

    private SequenceSpec BuildSequenceSpec(NodeInstance sequenceNode)
    {
        string sequenceName = ReadParam(sequenceNode, "SequenceName", "");
        if (string.IsNullOrWhiteSpace(sequenceName))
            throw new InvalidOperationException("SequenceDefinition requires SequenceName.");

        string defaultSchema = _provider == DatabaseProvider.SqlServer ? "dbo" : "public";
        string schema = ReadParam(sequenceNode, "Schema", defaultSchema);

        return new SequenceSpec(
            schema,
            sequenceName,
            ReadLongParam(sequenceNode, "StartValue"),
            ReadLongParam(sequenceNode, "Increment"),
            ReadLongParam(sequenceNode, "MinValue"),
            ReadLongParam(sequenceNode, "MaxValue"),
            ReadBoolParam(sequenceNode, "Cycle", false),
            ReadIntParam(sequenceNode, "Cache")
        );
    }

    private ScalarTypeSpec BuildScalarTypeSpec(NodeInstance scalarTypeNode)
    {
        string typeKind = ReadParam(scalarTypeNode, "TypeKind", "VARCHAR").Trim().ToUpperInvariant();
        int? length = ReadIntParam(scalarTypeNode, "Length");
        int? precision = ReadIntParam(scalarTypeNode, "Precision");
        int? scale = ReadIntParam(scalarTypeNode, "Scale");
        return new ScalarTypeSpec(typeKind, length, precision, scale);
    }

    private string ResolveSequenceDefaultExpression(SequenceSpec spec)
    {
        return _provider switch
        {
            DatabaseProvider.Postgres => $"nextval('{spec.Schema}.{spec.SequenceName}')",
            DatabaseProvider.SqlServer => $"NEXT VALUE FOR [{spec.Schema}].[{spec.SequenceName}]",
            DatabaseProvider.MySql or DatabaseProvider.SQLite => string.Empty,
            _ => string.Empty,
        };
    }

    private string ResolveEnumColumnDataType(EnumTypeSpec spec)
    {
        return _provider switch
        {
            DatabaseProvider.MySql => $"ENUM({string.Join(", ", spec.Values.Select(SqlStringUtility.QuoteLiteral))})",
            DatabaseProvider.Postgres => $"\"{spec.SchemaName.Replace("\"", "\"\"")}\".\"{spec.TypeName.Replace("\"", "\"\"")}\"",
            DatabaseProvider.SqlServer or DatabaseProvider.SQLite => "ENUM",
            _ => "ENUM",
        };
    }

    private string ResolveScalarColumnDataType(ScalarTypeSpec spec)
    {
        return spec.TypeKind switch
        {
            "VARCHAR" => $"VARCHAR({Math.Max(1, spec.Length ?? 255)})",
            "TEXT" => "TEXT",
            "INT" => "INT",
            "BIGINT" => "BIGINT",
            "DECIMAL" => $"DECIMAL({Math.Max(1, spec.Precision ?? 18)},{Math.Max(0, spec.Scale ?? 2)})",
            "BOOLEAN" => _provider == DatabaseProvider.SqlServer ? "BIT" : "BOOLEAN",
            "DATE" => "DATE",
            "DATETIME" => _provider == DatabaseProvider.Postgres ? "TIMESTAMP" : "DATETIME",
            "JSON" => _provider == DatabaseProvider.SqlServer ? "NVARCHAR(MAX)" : "JSON",
            "UUID" => _provider switch
            {
                DatabaseProvider.Postgres => "UUID",
                DatabaseProvider.SqlServer => "UNIQUEIDENTIFIER",
                _ => "CHAR(36)",
            },
            _ => spec.TypeKind,
        };
    }

    private static IReadOnlyList<string> ParseEnumValues(string rawValues)
    {
        if (string.IsNullOrWhiteSpace(rawValues))
            return [];

        return
        [
            .. rawValues
                .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim('\'', '"')),
        ];
    }

    private sealed record EnumTypeSpec(string SchemaName, string TypeName, IReadOnlyList<string> Values);
    private sealed record ScalarTypeSpec(string TypeKind, int? Length, int? Precision, int? Scale);
    private sealed record SequenceSpec(
        string Schema,
        string SequenceName,
        long? StartValue,
        long? Increment,
        long? MinValue,
        long? MaxValue,
        bool Cycle,
        int? Cache
    );

    private static string ReadParam(NodeInstance node, string name, string fallback)
    {
        if (!node.Parameters.TryGetValue(name, out string? value) || string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim();
    }

    private static string NormalizeViewSelect(NodeInstance viewNode)
    {
        string selectSql = ReadParam(viewNode, "SelectSql", "");
        if (string.IsNullOrWhiteSpace(selectSql))
            throw new InvalidOperationException("ViewDefinition requires SelectSql.");

        return selectSql.Trim().TrimEnd(';');
    }

    private string ResolveViewSelectSql(NodeInstance viewNode)
    {
        if (TryCompileViewSubgraphSql(viewNode, out string? sql) && !string.IsNullOrWhiteSpace(sql))
            return sql!;

        return NormalizeViewSelect(viewNode);
    }

    private bool TryCompileViewSubgraphSql(NodeInstance viewNode, out string? sql)
    {
        sql = null;
        string payload = ReadParam(viewNode, "ViewSubgraphGraphJson", "");
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        NodeGraph? subgraph;
        try
        {
            subgraph = JsonSerializer.Deserialize<NodeGraph>(payload);
        }
        catch
        {
            AddError(
                "E-DDL-VIEW-SUBGRAPH-JSON",
                "ViewDefinition.ViewSubgraphGraphJson inválido.",
                viewNode.Id
            );
            return false;
        }

        if (subgraph is null || subgraph.Nodes.Count == 0)
        {
            AddError(
                "E-DDL-VIEW-SUBGRAPH-EMPTY",
                "Subcanvas de view está vazio.",
                viewNode.Id
            );
            return false;
        }

        string fromTable = ReadParam(viewNode, "ViewFromTable", "");
        if (string.IsNullOrWhiteSpace(fromTable))
        {
            fromTable = subgraph.Nodes
                .FirstOrDefault(n => n.Type == NodeType.TableSource && !string.IsNullOrWhiteSpace(n.TableFullName))
                ?.TableFullName
                ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(fromTable))
        {
            AddError(
                "E-DDL-VIEW-SUBGRAPH-FROM",
                "Subcanvas de view requer ViewFromTable ou TableSource com TableFullName.",
                viewNode.Id
            );
            return false;
        }

        try
        {
            var queryService = QueryGeneratorService.Create(_provider);
            GeneratedQuery generated = queryService.Generate(fromTable, subgraph);
            sql = generated.Sql.Trim().TrimEnd(';');
            return true;
        }
        catch (Exception ex)
        {
            AddError(
                "E-DDL-VIEW-SUBGRAPH-COMPILE",
                ex.Message,
                viewNode.Id
            );
            return false;
        }
    }

    private static bool ReadBoolParam(NodeInstance node, string name, bool fallback)
    {
        if (!node.Parameters.TryGetValue(name, out string? value) || string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "on" => true,
            "false" or "0" or "no" or "off" => false,
            _ => fallback,
        };
    }

    private static DdlIdempotentMode ReadIdempotentMode(NodeInstance node)
    {
        string mode = ReadParam(node, "IdempotentMode", "None");
        return mode.Trim() switch
        {
            "IfNotExists" => DdlIdempotentMode.IfNotExists,
            "DropAndCreate" => DdlIdempotentMode.DropAndCreate,
            _ => DdlIdempotentMode.None,
        };
    }

    private DdlOutputCompilationContext CreateOutputCompilationContext() =>
        new(
            _graph,
            ReadIdempotentMode,
            CompileEnumTypeDefinition,
            CompileSequenceDefinition,
            CompileTableDefinition,
            CompileCreateTableAsOutput,
            CompileCreateViewDefinition,
            CompileAlterViewDefinition,
            CompileIndexDefinition,
            CompileAlterTableOutput,
            AddError
        );

    private static long? ReadLongParam(NodeInstance node, string name)
    {
        if (!node.Parameters.TryGetValue(name, out string? value) || string.IsNullOrWhiteSpace(value))
            return null;

        return long.TryParse(value.Trim(), out long parsed) ? parsed : null;
    }

    private static int? ReadIntParam(NodeInstance node, string name)
    {
        if (!node.Parameters.TryGetValue(name, out string? value) || string.IsNullOrWhiteSpace(value))
            return null;

        return int.TryParse(value.Trim(), out int parsed) ? parsed : null;
    }

    private void ValidateStaticRules(
        IReadOnlyList<NodeInstance> createTableOutputs,
        IReadOnlyList<NodeInstance> createTypeOutputs,
        IReadOnlyList<NodeInstance> createSequenceOutputs,
        IReadOnlyList<NodeInstance> createTableAsOutputs,
        IReadOnlyList<NodeInstance> createViewOutputs,
        IReadOnlyList<NodeInstance> alterViewOutputs)
    {
        ValidateCreateTypeOutputConnections(createTypeOutputs);
        ValidateCreateSequenceOutputConnections(createSequenceOutputs);
        ValidateCreateTableAsOutputConnections(createTableAsOutputs);
        ValidateCreateTableOutputConnections(createTableOutputs);
        ValidateCreateViewOutputConnections(createViewOutputs);
        ValidateAlterViewOutputConnections(alterViewOutputs);
        ValidateCreateTableHasColumns(createTableOutputs);
        ValidateColumnDefinitionNames();
        ValidateViewDefinitionNamesAndSelects();
        ValidateIdentityTypeCompatibility();
        ValidatePrimaryKeyMultiplicity();
        ValidateForeignKeyColumnSymmetry();
        ValidateForeignKeyCycles();
        ValidateProviderSpecificWarnings();
        ValidateEnumProviderWarnings();
        ValidatePostgresEnumTypeOutputs(createTypeOutputs);
        ValidateViewProviderWarnings();
        ValidateIdempotentModeWarnings(createTableOutputs, createTypeOutputs, createSequenceOutputs, createViewOutputs);
        ValidateLengthSensitiveTypes();
        ValidateDuplicateTableNames();
    }

    private void ValidateCreateTableAsOutputConnections(IReadOnlyList<NodeInstance> createTableAsOutputs)
    {
        foreach (NodeInstance output in createTableAsOutputs)
        {
            Connection? queryConn = _graph.GetSingleInputConnection(output.Id, "source_query");
            Connection? tableConn = _graph.GetSingleInputConnection(output.Id, "source_table");
            string selectSql = ReadParam(output, "SelectSql", "");

            if (queryConn is not null)
            {
                AddError(
                    "E-DDL-CREATETABLEAS-SOURCEQUERY-FORBIDDEN",
                    "Canvas DDL isolado: source_query nao pode receber conexao direta de nos de query.",
                    output.Id
                );
                continue;
            }

            if (tableConn is null && string.IsNullOrWhiteSpace(selectSql))
            {
                AddError(
                    "E-DDL-CREATETABLEAS-NO-SOURCE",
                    "CreateTableAsOutput requer source_table ou SelectSql.",
                    output.Id
                );
            }
        }
    }

    private void ValidateCreateSequenceOutputConnections(IReadOnlyList<NodeInstance> createSequenceOutputs)
    {
        foreach (NodeInstance output in createSequenceOutputs)
        {
            Connection? conn = _graph.GetSingleInputConnection(output.Id, "seq");
            if (conn is not null)
                continue;

            AddError(
                "E-DDL-OUTPUT-SEQUENCE-NOT-CONNECTED",
                "CreateSequenceOutput deve estar conectado a um SequenceDefinition",
                output.Id
            );
        }
    }

    private void ValidateIdempotentModeWarnings(
        IReadOnlyList<NodeInstance> createTableOutputs,
        IReadOnlyList<NodeInstance> createTypeOutputs,
        IReadOnlyList<NodeInstance> createSequenceOutputs,
        IReadOnlyList<NodeInstance> createViewOutputs)
    {
        foreach (NodeInstance output in createTableOutputs.Concat(createTypeOutputs).Concat(createSequenceOutputs).Concat(createViewOutputs))
        {
            DdlIdempotentMode mode = ReadIdempotentMode(output);

            if (mode == DdlIdempotentMode.DropAndCreate)
            {
                AddWarning(
                    "W-DDL-IDEMPOTENT-DROPANDCREATE",
                    "IdempotentMode=DropAndCreate inclui DROP destrutivo no script.",
                    output.Id
                );
            }

            if (output.Type == NodeType.CreateTypeOutput && mode == DdlIdempotentMode.DropAndCreate)
            {
                AddWarning(
                    "W-DDL-IDEMPOTENT-DROPTYPE-CASCADE",
                    "DROP TYPE IF EXISTS pode impactar colunas dependentes; avalie uso de CASCADE/ordem de execução.",
                    output.Id
                );
            }

            if (output.Type != NodeType.CreateViewOutput || mode != DdlIdempotentMode.IfNotExists)
                continue;

            if (_provider is DatabaseProvider.MySql or DatabaseProvider.SqlServer)
            {
                AddWarning(
                    "W-DDL-IDEMPOTENT-IFNOTEXISTS-VIEW-UNSUPPORTED",
                    $"Provider {_provider} não suporta CREATE VIEW IF NOT EXISTS nativo; modo será degradado.",
                    output.Id
                );
            }
        }
    }

    private void ValidateCreateViewOutputConnections(IReadOnlyList<NodeInstance> createViewOutputs)
    {
        foreach (NodeInstance output in createViewOutputs)
        {
            Connection? conn = _graph.GetSingleInputConnection(output.Id, "view");
            if (conn is not null)
                continue;

            AddError(
                "E-DDL-OUTPUT-VIEW-NOT-CONNECTED",
                "CreateViewOutput deve estar conectado a um ViewDefinition",
                output.Id
            );
        }
    }

    private void ValidateAlterViewOutputConnections(IReadOnlyList<NodeInstance> alterViewOutputs)
    {
        foreach (NodeInstance output in alterViewOutputs)
        {
            Connection? conn = _graph.GetSingleInputConnection(output.Id, "view");
            if (conn is not null)
                continue;

            AddError(
                "E-DDL-ALTERVIEW-OUTPUT-NOT-CONNECTED",
                "AlterViewOutput deve estar conectado a um ViewDefinition",
                output.Id
            );
        }
    }

    private void ValidateCreateTypeOutputConnections(IReadOnlyList<NodeInstance> createTypeOutputs)
    {
        foreach (NodeInstance output in createTypeOutputs)
        {
            Connection? conn = _graph.GetSingleInputConnection(output.Id, "type_def");
            if (conn is not null)
                continue;

            AddError(
                "E-DDL-OUTPUT-TYPEDEF-NOT-CONNECTED",
                "Nó de saída de tipo não conectado a nenhum EnumTypeDefinition",
                output.Id
            );
        }
    }

    private void ValidateCreateTableOutputConnections(IReadOnlyList<NodeInstance> createTableOutputs)
    {
        foreach (NodeInstance output in createTableOutputs)
        {
            Connection? conn = _graph.GetSingleInputConnection(output.Id, "table");
            if (conn is not null)
                continue;

            AddError(
                "E-DDL-OUTPUT-TABLE-NOT-CONNECTED",
                "Nó de saída não conectado a nenhuma tabela",
                output.Id
            );
        }
    }

    private void ValidateCreateTableHasColumns(IReadOnlyList<NodeInstance> createTableOutputs)
    {
        var tableIds = createTableOutputs
            .Select(outNode => _graph.GetSingleInputConnection(outNode.Id, "table")?.FromNodeId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (string tableId in tableIds)
        {
            int columnCount = _graph.Connections.Count(c =>
                c.ToNodeId == tableId
                && c.ToPinName == "column"
                && _graph.NodeMap.TryGetValue(c.FromNodeId, out NodeInstance? from)
                && from.Type == NodeType.ColumnDefinition
            );

            if (columnCount > 0)
                continue;

            AddError("E-DDL-TABLE-NO-COLUMNS", "Tabela sem colunas definidas", tableId);
        }
    }

    private void ValidateColumnDefinitionNames()
    {
        foreach (NodeInstance col in _graph.Nodes.Where(n => n.Type == NodeType.ColumnDefinition))
        {
            string columnName = ReadParam(col, "ColumnName", "");
            if (!string.IsNullOrWhiteSpace(columnName))
                continue;

            AddError("E-DDL-COLUMN-NAME-BLANK", "Nome de coluna não pode estar em branco", col.Id);
        }
    }

    private void ValidateViewDefinitionNamesAndSelects()
    {
        foreach (NodeInstance view in _graph.Nodes.Where(n => n.Type == NodeType.ViewDefinition))
        {
            string viewName = ReadParam(view, "ViewName", "");
            if (string.IsNullOrWhiteSpace(viewName))
                AddError("E-DDL-VIEW-NAME-BLANK", "Nome da view não pode estar em branco", view.Id);

            string selectSql = ReadParam(view, "SelectSql", "");
            string subgraph = ReadParam(view, "ViewSubgraphGraphJson", "");
            if (string.IsNullOrWhiteSpace(selectSql) && string.IsNullOrWhiteSpace(subgraph))
                AddError("E-DDL-VIEW-SELECT-BLANK", "ViewDefinition requer SELECT compilado", view.Id);
        }
    }

    private void ValidateIdentityTypeCompatibility()
    {
        foreach (NodeInstance col in _graph.Nodes.Where(n => n.Type == NodeType.ColumnDefinition))
        {
            if (!ReadBoolParam(col, "IsIdentity", false))
                continue;

            string dataType = ReadParam(col, "DataType", "INT");
            if (IsIntegerDataType(dataType))
                continue;

            AddError(
                "E-DDL-IDENTITY-NONINTEGER",
                "Identity/AutoIncrement só é válido em colunas inteiras",
                col.Id
            );
        }
    }

    private void ValidatePrimaryKeyMultiplicity()
    {
        foreach (NodeInstance table in _graph.Nodes.Where(n => n.Type == NodeType.TableDefinition))
        {
            int pkCount = _graph.Connections
                .Where(c => c.ToNodeId == table.Id && c.ToPinName == "constraint")
                .Select(c => _graph.NodeMap[c.FromNodeId])
                .Count(n => n.Type == NodeType.PrimaryKeyConstraint);

            if (pkCount <= 1)
                continue;

            AddError(
                "E-DDL-PK-DUPLICATE",
                "Tabela não pode ter mais de uma PK",
                table.Id
            );
        }
    }

    private void ValidateForeignKeyColumnSymmetry()
    {
        foreach (NodeInstance fk in _graph.Nodes.Where(n => n.Type == NodeType.ForeignKeyConstraint))
        {
            int childCount = _graph.GetInputConnections(fk.Id, "child_column").Count;
            int parentCount = _graph.GetInputConnections(fk.Id, "parent_column").Count;
            if (childCount == parentCount)
                continue;

            AddError(
                "E-DDL-FK-COLUMN-COUNT",
                "FK composta: número de colunas filho ≠ colunas pai",
                fk.Id
            );
        }
    }

    private void ValidateForeignKeyCycles()
    {
        var columnToTable = _graph.Connections
            .Where(c => c.ToPinName == "column"
                && _graph.NodeMap.TryGetValue(c.FromNodeId, out NodeInstance? from)
                && from.Type == NodeType.ColumnDefinition
                && _graph.NodeMap.TryGetValue(c.ToNodeId, out NodeInstance? to)
                && to.Type == NodeType.TableDefinition)
            .ToDictionary(c => c.FromNodeId, c => c.ToNodeId, StringComparer.Ordinal);

        var edges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (NodeInstance fk in _graph.Nodes.Where(n => n.Type == NodeType.ForeignKeyConstraint))
        {
            string? childTableId = _graph.Connections
                .FirstOrDefault(c => c.FromNodeId == fk.Id && c.ToPinName == "constraint")
                ?.ToNodeId;
            if (string.IsNullOrWhiteSpace(childTableId))
                continue;

            string? parentTableId = _graph.GetInputConnections(fk.Id, "parent_column")
                .Select(c => c.FromNodeId)
                .Where(columnToTable.ContainsKey)
                .Select(colId => columnToTable[colId])
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(parentTableId) || parentTableId == childTableId)
                continue;

            if (!edges.TryGetValue(childTableId, out HashSet<string>? targets))
            {
                targets = new HashSet<string>(StringComparer.Ordinal);
                edges[childTableId] = targets;
            }

            targets.Add(parentTableId);
        }

        if (!HasDirectedCycle(edges))
            return;

        AddWarning(
            "W-DDL-FK-CYCLE",
            "Dependência circular de FK detectada — verifique a ordem de criação"
        );
    }

    private void ValidateProviderSpecificWarnings()
    {
        if (_provider == DatabaseProvider.SQLite)
        {
            foreach (NodeInstance op in _graph.Nodes.Where(n => n.Type == NodeType.AlterColumnTypeOp))
            {
                AddWarning(
                    "W-DDL-ALTERTYPE-SQLITE",
                    "SQLite não suporta ALTER COLUMN TYPE — considere recriar a tabela",
                    op.Id
                );
            }

            foreach (NodeInstance table in _graph.Nodes.Where(n => n.Type == NodeType.TableDefinition))
            {
                string tableComment = ReadParam(table, "Comment", "");
                if (!string.IsNullOrWhiteSpace(tableComment))
                {
                    AddWarning(
                        "W-DDL-COMMENT-SQLITE",
                        "SQLite não possui suporte nativo a comentários de tabela/coluna.",
                        table.Id
                    );
                }
            }

            foreach (NodeInstance col in _graph.Nodes.Where(n => n.Type == NodeType.ColumnDefinition))
            {
                string columnComment = ReadParam(col, "Comment", "");
                if (!string.IsNullOrWhiteSpace(columnComment))
                {
                    AddWarning(
                        "W-DDL-COMMENT-SQLITE",
                        "SQLite não possui suporte nativo a comentários de tabela/coluna.",
                        col.Id
                    );
                }
            }
        }

        if (_provider == DatabaseProvider.SqlServer)
        {
            foreach (NodeInstance op in _graph.Nodes.Where(n => n.Type == NodeType.DropColumnOp))
            {
                if (!ReadBoolParam(op, "IfExists", false))
                    continue;

                AddWarning(
                    "W-DDL-DROPCOLUMN-IFEXISTS-SQLSERVER",
                    "SQL Server não suporta IF EXISTS em DROP COLUMN — será removido",
                    op.Id
                );
            }
        }

        if (_provider is DatabaseProvider.MySql or DatabaseProvider.SQLite)
        {
            foreach (NodeInstance seq in _graph.Nodes.Where(n => n.Type == NodeType.SequenceDefinition))
            {
                AddWarning(
                    "W-DDL-SEQUENCE-UNSUPPORTED-PROVIDER",
                    $"Provider {_provider} não possui suporte nativo a CREATE SEQUENCE.",
                    seq.Id
                );
            }
        }

        foreach (NodeInstance op in _graph.Nodes.Where(n => n.Type == NodeType.DropTableOp))
        {
            AddWarning(
                "W-DDL-DROPTABLE-DESTRUCTIVE",
                "DROP TABLE é destrutivo e irreversível; confirme antes de executar.",
                op.Id
            );
        }

        if (_provider is DatabaseProvider.MySql or DatabaseProvider.SQLite)
        {
            foreach (NodeInstance op in _graph.Nodes.Where(n => n.Type == NodeType.RenameTableOp))
            {
                string newSchema = ReadParam(op, "NewSchema", "");
                if (string.IsNullOrWhiteSpace(newSchema))
                    continue;

                AddWarning(
                    "W-DDL-RENAMETABLE-NEWSCHEMA-UNSUPPORTED",
                    $"Provider {_provider} não suporta troca de schema no RenameTableOp.",
                    op.Id
                );
            }
        }
    }

    private void ValidateEnumProviderWarnings()
    {
        if (_provider is not DatabaseProvider.SqlServer and not DatabaseProvider.SQLite)
            return;

        foreach (NodeInstance col in _graph.Nodes.Where(n => n.Type == NodeType.ColumnDefinition))
        {
            bool hasTypeDefConnection = _graph.GetSingleInputConnection(col.Id, "type_def") is not null;
            string dataType = ReadParam(col, "DataType", "INT");
            bool usesEnumToken = dataType.Trim().StartsWith("ENUM", StringComparison.OrdinalIgnoreCase);

            if (!hasTypeDefConnection && !usesEnumToken)
                continue;

            AddWarning(
                "W-DDL-ENUM-UNSUPPORTED-PROVIDER",
                $"Provider {_provider} não possui suporte nativo a ENUM.",
                col.Id
            );
        }
    }

    private void ValidatePostgresEnumTypeOutputs(IReadOnlyList<NodeInstance> createTypeOutputs)
    {
        if (_provider != DatabaseProvider.Postgres)
            return;

        HashSet<string> emittedTypeNodeIds =
        [
            .. createTypeOutputs
                .Select(output => _graph.GetSingleInputConnection(output.Id, "type_def")?.FromNodeId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>(),
        ];

        foreach (NodeInstance col in _graph.Nodes.Where(n => n.Type == NodeType.ColumnDefinition))
        {
            Connection? typeConn = _graph.GetSingleInputConnection(col.Id, "type_def");
            if (typeConn is null)
                continue;

            NodeInstance typeNode = _graph.NodeMap[typeConn.FromNodeId];
            if (typeNode.Type != NodeType.EnumTypeDefinition)
                continue;

            if (emittedTypeNodeIds.Contains(typeConn.FromNodeId))
                continue;

            AddError(
                "E-DDL-ENUM-TYPE-NOT-EMITTED",
                "EnumTypeDefinition conectado à coluna requer CreateTypeOutput no PostgreSQL.",
                col.Id
            );
        }
    }

    private void ValidateViewProviderWarnings()
    {
        foreach (NodeInstance view in _graph.Nodes.Where(n => n.Type == NodeType.ViewDefinition))
        {
            bool isMaterialized = ReadBoolParam(view, "IsMaterialized", false);
            bool orReplace = ReadBoolParam(view, "OrReplace", false);

            if (isMaterialized && _provider is DatabaseProvider.SqlServer or DatabaseProvider.MySql or DatabaseProvider.SQLite)
            {
                AddError(
                    "E-DDL-VIEW-MATERIALIZED-UNSUPPORTED",
                    $"Provider {_provider} não suporta MATERIALIZED VIEW.",
                    view.Id
                );
            }

            if (_provider == DatabaseProvider.Postgres && isMaterialized && orReplace)
            {
                AddError(
                    "E-DDL-VIEW-MATERIALIZED-ORREPLACE",
                    "PostgreSQL não suporta CREATE OR REPLACE MATERIALIZED VIEW.",
                    view.Id
                );
            }
        }

        if (_provider == DatabaseProvider.SQLite)
        {
            foreach (NodeInstance output in _graph.Nodes.Where(n => n.Type == NodeType.AlterViewOutput))
            {
                AddWarning(
                    "W-DDL-ALTERVIEW-SQLITE",
                    "SQLite não suporta ALTER VIEW; será emitido DROP VIEW IF EXISTS + CREATE VIEW.",
                    output.Id
                );
            }
        }
    }

    private void ValidateLengthSensitiveTypes()
    {
        foreach (NodeInstance col in _graph.Nodes.Where(n => n.Type == NodeType.ColumnDefinition))
        {
            string dataType = ReadParam(col, "DataType", "INT");
            if (!NeedsLengthButMissing(dataType))
                continue;

            AddWarning(
                "W-DDL-TYPE-LENGTH-MISSING",
                "VARCHAR sem tamanho — padrão será aplicado (1 no SQL Server, sem limite no Postgres)",
                col.Id
            );
        }
    }

    private void ValidateDuplicateTableNames()
    {
        var names = new List<(string NodeId, string Name)>();

        foreach (NodeInstance table in _graph.Nodes.Where(n => n.Type == NodeType.TableDefinition))
        {
            string schema = ReadParam(table, "SchemaName", "public");
            string tableName = ReadParam(table, "TableName", "");
            if (string.IsNullOrWhiteSpace(tableName))
                continue;

            names.Add((table.Id, $"{schema}.{tableName}"));
        }

        foreach (NodeInstance tableRef in _graph.Nodes.Where(n => n.Type == NodeType.TableSource && !string.IsNullOrWhiteSpace(n.TableFullName)))
            names.Add((tableRef.Id, tableRef.TableFullName!));

        foreach (IGrouping<string, (string NodeId, string Name)> dup in names
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1))
        {
            foreach ((string nodeId, _) in dup)
                AddWarning("W-DDL-TABLE-NAME-DUPLICATE", "Nome de tabela duplicado no canvas", nodeId);
        }
    }

    private static bool NeedsLengthButMissing(string dataType)
    {
        string normalized = dataType.Trim();
        if (normalized.Contains('('))
            return false;

        return Regex.IsMatch(normalized, "^(VAR)?CHAR$|^NCHAR$|^NVARCHAR$", RegexOptions.IgnoreCase);
    }

    private static bool IsIntegerDataType(string dataType)
    {
        string normalized = dataType.Trim();
        int idx = normalized.IndexOf('(');
        if (idx > 0)
            normalized = normalized[..idx];

        normalized = normalized.ToUpperInvariant();
        return normalized is "INT" or "INTEGER" or "BIGINT" or "SMALLINT" or "TINYINT";
    }

    private static bool HasDirectedCycle(IReadOnlyDictionary<string, HashSet<string>> edges)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var stack = new HashSet<string>(StringComparer.Ordinal);

        bool Dfs(string node)
        {
            if (!visited.Add(node))
                return stack.Contains(node);

            stack.Add(node);
            if (edges.TryGetValue(node, out HashSet<string>? nextNodes))
            {
                foreach (string next in nextNodes)
                {
                    if (Dfs(next))
                        return true;
                }
            }

            stack.Remove(node);
            return false;
        }

        foreach (string node in edges.Keys)
        {
            if (Dfs(node))
                return true;
        }

        return false;
    }

    private void AddWarning(string code, string message, string? nodeId = null)
    {
        _diagnostics.Add(new DdlCompileDiagnostic(code, DdlDiagnosticSeverity.Warning, message, nodeId));
    }

    private void AddError(string code, string message, string? nodeId = null)
    {
        _diagnostics.Add(new DdlCompileDiagnostic(code, DdlDiagnosticSeverity.Error, message, nodeId));
    }
}

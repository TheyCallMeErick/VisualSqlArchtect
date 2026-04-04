using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.Services.SqlImport.Build;
using VisualSqlArchitect.UI.Services.SqlImport.Execution.Parsing;
using VisualSqlArchitect.UI.Services.SqlImport.Rewriting;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services.SqlImport.Execution.Applying;

internal sealed class SqlImportWhereClauseApplier(CanvasViewModel canvas) : ISqlImportApplyStep
{
    private readonly CanvasViewModel _canvas = canvas;

    public SqlImportApplyResult Apply(SqlImportApplyContext context)
    {
        SqlImportParsedQuery query = context.Query;
        ImportBuildContext coreContext = context.CoreContext;
        ObservableCollection<ImportReportItem> report = context.Report;
        var layout = new SqlImportLayoutCalculator(coreContext.Layout);

        if (query.WhereClause is null)
            return new SqlImportApplyResult(0, 0, 0);

        int imported = 0;
        int partial = 0;

        var tableNodes = coreContext.TableNodes;
        NodeViewModel result = coreContext.ResultNode;
        int sourceCount = query.FromParts.Count;

        Match existsMatch = Regex.Match(
            query.WhereClause,
            @"^EXISTS\s*\((SELECT.+)\)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        if (existsMatch.Success)
        {
            string subquerySql = existsMatch.Groups[1].Value.Trim();
            string correlatedFields = SqlImportCteRewriteService.DescribeCorrelatedOuterReferences(subquerySql, query.OuterAliases);
            NodeViewModel existsNode = new(
                NodeDefinitionRegistry.Get(NodeType.SubqueryExists),
                layout.SubqueryPosition(sourceCount)
            );
            existsNode.Parameters["query"] = subquerySql;
            _canvas.Nodes.Add(existsNode);
            SqlImportClauseApplyUtilities.SafeWire(existsNode, "result", result, "where", _canvas);

            report.Add(new ImportReportItem("WHERE EXISTS(sub-query)", EImportItemStatus.Imported, sourceNodeId: existsNode.Id));
            imported++;

            if (!string.IsNullOrWhiteSpace(correlatedFields))
            {
                report.Add(
                    new ImportReportItem(
                        "Correlation fields",
                        EImportItemStatus.Imported,
                        $"External references: {correlatedFields}",
                        existsNode.Id
                    )
                );
                imported++;
            }

            return new SqlImportApplyResult(imported, partial, 0);
        }

        Match inSubqueryMatch = Regex.Match(
            query.WhereClause,
            @"^(\w+(?:\.\w+)?)\s+(NOT\s+)?IN\s*\((SELECT.+)\)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        if (inSubqueryMatch.Success)
        {
            string leftExpr = inSubqueryMatch.Groups[1].Value.Trim().Split('.').Last();
            bool negate = inSubqueryMatch.Groups[2].Success;
            string subquerySql = inSubqueryMatch.Groups[3].Value.Trim();
            string correlatedFields = SqlImportCteRewriteService.DescribeCorrelatedOuterReferences(subquerySql, query.OuterAliases);

            NodeViewModel inNode = new(
                NodeDefinitionRegistry.Get(NodeType.SubqueryIn),
                layout.SubqueryPosition(sourceCount)
            );
            inNode.Parameters["query"] = subquerySql;
            inNode.Parameters["negate"] = negate ? "true" : "false";
            _canvas.Nodes.Add(inNode);

            PinViewModel? valuePin = tableNodes
                .SelectMany(n => n.OutputPins)
                .FirstOrDefault(p => p.Name.Equals(leftExpr, StringComparison.OrdinalIgnoreCase));
            if (valuePin is not null)
                SqlImportClauseApplyUtilities.SafeWire(valuePin.Owner, valuePin.Name, inNode, "value", _canvas);

            SqlImportClauseApplyUtilities.SafeWire(inNode, "result", result, "where", _canvas);

            report.Add(
                new ImportReportItem(
                    negate ? "WHERE value NOT IN(sub-query)" : "WHERE value IN(sub-query)",
                    EImportItemStatus.Imported,
                    sourceNodeId: inNode.Id
                )
            );
            imported++;

            if (!string.IsNullOrWhiteSpace(correlatedFields))
            {
                report.Add(
                    new ImportReportItem(
                        "Correlation fields",
                        EImportItemStatus.Imported,
                        $"External references: {correlatedFields}",
                        inNode.Id
                    )
                );
                imported++;
            }

            return new SqlImportApplyResult(imported, partial, 0);
        }

        Match scalarSubqueryMatch = Regex.Match(
            query.WhereClause,
            @"^(\w+(?:\.\w+)?)\s*(=|<>|!=|>|>=|<|<=)\s*\((SELECT.+)\)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        if (scalarSubqueryMatch.Success)
        {
            string leftExpr = scalarSubqueryMatch.Groups[1].Value.Trim().Split('.').Last();
            string op = scalarSubqueryMatch.Groups[2].Value.Trim();
            string subquerySql = scalarSubqueryMatch.Groups[3].Value.Trim();
            string correlatedFields = SqlImportCteRewriteService.DescribeCorrelatedOuterReferences(subquerySql, query.OuterAliases);

            NodeViewModel scalarNode = new(
                NodeDefinitionRegistry.Get(NodeType.SubqueryScalar),
                layout.SubqueryPosition(sourceCount)
            );
            scalarNode.Parameters["query"] = subquerySql;
            scalarNode.Parameters["operator"] = op == "!=" ? "<>" : op;
            _canvas.Nodes.Add(scalarNode);

            PinViewModel? leftPin = tableNodes
                .SelectMany(n => n.OutputPins)
                .FirstOrDefault(p => p.Name.Equals(leftExpr, StringComparison.OrdinalIgnoreCase));
            if (leftPin is not null)
                SqlImportClauseApplyUtilities.SafeWire(leftPin.Owner, leftPin.Name, scalarNode, "left", _canvas);

            SqlImportClauseApplyUtilities.SafeWire(scalarNode, "result", result, "where", _canvas);

            report.Add(
                new ImportReportItem(
                    "WHERE value op (scalar sub-query)",
                    EImportItemStatus.Imported,
                    sourceNodeId: scalarNode.Id
                )
            );
            imported++;

            if (!string.IsNullOrWhiteSpace(correlatedFields))
            {
                report.Add(
                    new ImportReportItem(
                        "Correlation fields",
                        EImportItemStatus.Imported,
                        $"External references: {correlatedFields}",
                        scalarNode.Id
                    )
                );
                imported++;
            }

            return new SqlImportApplyResult(imported, partial, 0);
        }

        Match eqMatch = Regex.Match(
            query.WhereClause,
            @"^(\w+(?:\.\w+)?)\s*(=|<>|!=|>|>=|<|<=)\s*(.+)$",
            RegexOptions.IgnoreCase
        );

        if (eqMatch.Success && !Regex.IsMatch(query.WhereClause, @"\b(AND|OR)\b", RegexOptions.IgnoreCase))
        {
            string leftExpr = eqMatch.Groups[1].Value.Trim().Split('.').Last();
            string op = eqMatch.Groups[2].Value.Trim();
            string rightExpr = eqMatch.Groups[3].Value.Trim().Trim('\'', '"');

            NodeType compType = op switch
            {
                "=" => NodeType.Equals,
                "<>" or "!=" => NodeType.NotEquals,
                ">" => NodeType.GreaterThan,
                ">=" => NodeType.GreaterOrEqual,
                "<" => NodeType.LessThan,
                "<=" => NodeType.LessOrEqual,
                _ => NodeType.Equals,
            };

            NodeViewModel comp = new(
                NodeDefinitionRegistry.Get(compType),
                layout.ComparisonPosition(sourceCount)
            );
            comp.PinLiterals["right"] = rightExpr;
            _canvas.Nodes.Add(comp);

            NodeViewModel where = new(
                NodeDefinitionRegistry.Get(NodeType.WhereOutput),
                layout.WherePosition(sourceCount)
            );
            _canvas.Nodes.Add(where);

            PinViewModel? leftPin = tableNodes
                .SelectMany(n => n.OutputPins)
                .FirstOrDefault(p => p.Name.Equals(leftExpr, StringComparison.OrdinalIgnoreCase));
            if (leftPin is not null)
                SqlImportClauseApplyUtilities.SafeWire(leftPin.Owner, leftPin.Name, comp, "left", _canvas);

            SqlImportClauseApplyUtilities.SafeWire(comp, "result", where, "condition", _canvas);
            SqlImportClauseApplyUtilities.SafeWire(where, "result", result, "where", _canvas);

            report.Add(
                new ImportReportItem(
                    $"WHERE {leftExpr} {op} '{rightExpr}'",
                    EImportItemStatus.Imported,
                    sourceNodeId: where.Id
                )
            );
            imported++;
        }
        else
        {
            NodeViewModel where = new(
                NodeDefinitionRegistry.Get(NodeType.WhereOutput),
                layout.WherePosition(sourceCount)
            );
            _canvas.Nodes.Add(where);

            report.Add(
                new ImportReportItem(
                    $"WHERE {SqlImportClauseApplyUtilities.Truncate(query.WhereClause, 40)}",
                    EImportItemStatus.Partial,
                    "Complex condition - connect manually",
                    where.Id
                )
            );
            partial++;
        }

        return new SqlImportApplyResult(imported, partial, 0);
    }
}

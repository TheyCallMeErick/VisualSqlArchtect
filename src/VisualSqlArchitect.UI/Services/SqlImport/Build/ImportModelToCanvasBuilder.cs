using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Avalonia;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services.SqlImport.Build;

public readonly record struct ImportFromPart(string Table, string? JoinType, string? OnClause);

public readonly record struct ImportSelectTerm(string Expr, string? Alias);

public readonly record struct ImportLayout(double BaseX, double BaseY, double ColGap, double RowGap);

public sealed record ImportBuildInput(
    IReadOnlyList<ImportFromPart> FromParts,
    IReadOnlyList<ImportSelectTerm> SelectedColumns,
    bool IsStar,
    ImportLayout Layout
);

public sealed record ImportBuildContext(
    List<NodeViewModel> TableNodes,
    NodeViewModel ResultNode,
    Dictionary<string, PinViewModel> ProjectedAliases,
    ImportLayout Layout,
    double ResultY,
    int Imported,
    int Partial,
    int Skipped
);

public sealed class ImportModelToCanvasBuilder(CanvasViewModel canvas)
{
    private readonly CanvasViewModel _canvas = canvas;
    private readonly IReadOnlyList<IImportBuildStep> _steps =
    [
        new ImportBuildResetCanvasStep(),
        new ImportBuildSourceTablesStep(),
        new ImportBuildJoinNodesStep(),
        new ImportBuildProjectionStep(),
    ];

    public ImportBuildContext BuildCore(
        ImportBuildInput input,
        ObservableCollection<ImportReportItem> report,
        CancellationToken ct
    )
    {
        var state = new ImportBuildState();
        var context = new ImportBuildPipelineContext(_canvas, input, report, ct, state);

        foreach (IImportBuildStep step in _steps)
            step.Apply(context);

        return new ImportBuildContext(
            state.TableNodes,
            state.ResultNode!,
            state.ProjectedAliases,
            input.Layout,
            state.ResultY,
            state.Imported,
            state.Partial,
            state.Skipped
        );
    }

    private sealed class ImportBuildState
    {
        public List<NodeViewModel> TableNodes { get; } = [];
        public Dictionary<string, PinViewModel> ProjectedAliases { get; } = new(StringComparer.OrdinalIgnoreCase);
        public NodeViewModel? ResultNode { get; set; }
        public double ResultY { get; set; }
        public int Imported { get; set; }
        public int Partial { get; set; }
        public int Skipped { get; set; }
    }

    private sealed record ImportBuildPipelineContext(
        CanvasViewModel Canvas,
        ImportBuildInput Input,
        ObservableCollection<ImportReportItem> Report,
        CancellationToken CancellationToken,
        ImportBuildState State
    );

    private interface IImportBuildStep
    {
        void Apply(ImportBuildPipelineContext context);
    }

    private sealed class ImportBuildResetCanvasStep : IImportBuildStep
    {
        public void Apply(ImportBuildPipelineContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            context.Canvas.Connections.Clear();
            context.Canvas.Nodes.Clear();
            context.Canvas.UndoRedo.Clear();
        }
    }

    private sealed class ImportBuildSourceTablesStep : IImportBuildStep
    {
        public void Apply(ImportBuildPipelineContext context)
        {
            var calculator = new SqlImportLayoutCalculator(context.Input.Layout);

            for (int i = 0; i < context.Input.FromParts.Count; i++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                string table = context.Input.FromParts[i].Table;
                Point pos = calculator.TablePosition(i);

                var catalogEntry = CanvasViewModel.DemoCatalog.FirstOrDefault(t =>
                    t.FullName.Equals(table, StringComparison.OrdinalIgnoreCase)
                    || t.FullName.EndsWith("." + table, StringComparison.OrdinalIgnoreCase)
                );

                NodeViewModel tableNode =
                    catalogEntry != default
                        ? new NodeViewModel(catalogEntry.FullName, catalogEntry.Cols, pos)
                        : new NodeViewModel(table, [], pos);

                context.Canvas.Nodes.Add(tableNode);
                context.State.TableNodes.Add(tableNode);

                context.Report.Add(
                    new ImportReportItem(
                        context.Input.FromParts[i].JoinType is not null
                            ? $"{context.Input.FromParts[i].JoinType}: {table}"
                            : $"FROM: {table}",
                        EImportItemStatus.Imported,
                        sourceNodeId: tableNode.Id
                    )
                );
                context.State.Imported++;
            }
        }
    }

    private sealed class ImportBuildJoinNodesStep : IImportBuildStep
    {
        public void Apply(ImportBuildPipelineContext context)
        {
            var calculator = new SqlImportLayoutCalculator(context.Input.Layout);

            for (int i = 1; i < context.Input.FromParts.Count; i++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (
                    string.IsNullOrWhiteSpace(context.Input.FromParts[i].JoinType)
                    || string.IsNullOrWhiteSpace(context.Input.FromParts[i].OnClause)
                )
                {
                    continue;
                }

                Match onMatch = Regex.Match(
                    context.Input.FromParts[i].OnClause!,
                    @"^\s*([\w\.]+)\s*(=|<>|!=|>|>=|<|<=)\s*([\w\.]+)\s*$",
                    RegexOptions.IgnoreCase
                );

                if (!onMatch.Success)
                    continue;

                NodeViewModel joinNode = new(
                    NodeDefinitionRegistry.Get(NodeType.Join),
                    calculator.JoinPosition(i)
                );
                joinNode.Parameters["join_type"] = ImportBuildUtilities.NormalizeJoinType(context.Input.FromParts[i].JoinType!);
                joinNode.Parameters["right_source"] = context.Input.FromParts[i].Table;
                joinNode.Parameters["left_expr"] = onMatch.Groups[1].Value.Trim();
                joinNode.Parameters["operator"] = onMatch.Groups[2].Value.Trim() == "!="
                    ? "<>"
                    : onMatch.Groups[2].Value.Trim();
                joinNode.Parameters["right_expr"] = onMatch.Groups[3].Value.Trim();
                context.Canvas.Nodes.Add(joinNode);
            }
        }
    }

    private sealed class ImportBuildProjectionStep : IImportBuildStep
    {
        public void Apply(ImportBuildPipelineContext context)
        {
            if (context.State.TableNodes.Count == 0)
                throw new InvalidOperationException("Cannot build projection without source tables.");

            var calculator = new SqlImportLayoutCalculator(context.Input.Layout);
            context.State.ResultY = calculator.ResultY(context.Input.FromParts.Count);

            NodeViewModel result = new(
                NodeDefinitionRegistry.Get(NodeType.ResultOutput),
                calculator.ResultPosition(context.State.ResultY)
            );
            result.Parameters.Remove("import_order_terms");
            result.Parameters.Remove("import_group_terms");
            context.Canvas.Nodes.Add(result);
            context.State.ResultNode = result;

            NodeViewModel columnList = new(
                NodeDefinitionRegistry.Get(NodeType.ColumnSetBuilder),
                calculator.ColumnSetPosition(context.State.ResultY)
            );
            context.Canvas.Nodes.Add(columnList);
            ImportBuildUtilities.SafeWire(columnList, "result", result, "columns", context.Canvas);

            NodeViewModel primaryNode = context.State.TableNodes[0];
            if (context.Input.IsStar)
            {
                foreach (PinViewModel pin in primaryNode.OutputPins)
                    ImportBuildUtilities.SafeWire(primaryNode, pin.Name, columnList, "columns", context.Canvas);

                context.Report.Add(
                    new ImportReportItem(
                        "SELECT *",
                        EImportItemStatus.Imported,
                        "All columns wired",
                        result.Id
                    )
                );
                context.State.Imported++;
                return;
            }

            if (context.Input.SelectedColumns.Count == 0)
                return;

            int wired = 0;
            foreach ((string expr, string? alias) in context.Input.SelectedColumns)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                string colName = expr.Split('.').Last().Trim();
                PinViewModel? pin = context.State.TableNodes
                    .SelectMany(n => n.OutputPins)
                    .FirstOrDefault(p => p.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));

                if (pin is not null)
                {
                    ImportBuildUtilities.SafeWire(pin.Owner, pin.Name, columnList, "columns", context.Canvas);
                    if (!string.IsNullOrWhiteSpace(alias))
                        context.State.ProjectedAliases[alias.Trim()] = pin;
                    wired++;
                }
                else
                {
                    context.Report.Add(
                        new ImportReportItem(
                            $"Column: {expr}",
                            EImportItemStatus.Skipped,
                            "Complex expression — wire manually"
                        )
                    );
                    context.State.Skipped++;
                }
            }

            context.Report.Add(
                new ImportReportItem(
                    $"SELECT ({wired}/{context.Input.SelectedColumns.Count} columns)",
                    wired == context.Input.SelectedColumns.Count
                        ? EImportItemStatus.Imported
                        : EImportItemStatus.Partial,
                    wired == context.Input.SelectedColumns.Count
                        ? null
                        : "Some selected columns could not be auto-wired.",
                    result.Id
                )
            );

            if (wired == context.Input.SelectedColumns.Count)
                context.State.Imported++;
            else
                context.State.Partial++;
        }
    }
}

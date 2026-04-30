using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using AkkornStudio.Core;
using AkkornStudio.UI.Services.Theming;
using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.UI.Services.SqlEditor.Reports;

internal static class SqlEditorReportHtmlBuilder
{
    private const string ReportVersion = "3.2";

    public static string Build(SqlEditorReportExportContext context, SqlEditorReportExportRequest request)
    {
        string language = ResolveLanguage();
        string title = string.IsNullOrWhiteSpace(request.Title) ? context.TabTitle : request.Title.Trim();
        string description = string.IsNullOrWhiteSpace(request.Description) ? string.Empty : request.Description.Trim();
        string generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        string generatedAtIso = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);

        object meta = BuildMeta(context, request, generatedAt, generatedAtIso, title, description);
        IReadOnlyList<SqlEditorReportSchemaDetail> schemaRows = request.IncludeSchema ? BuildSchemaRows(context) : [];
        IReadOnlyList<object> metadataRows = BuildMetadataRows(context, request, generatedAtIso, language);
        IReadOnlyList<object> lineageNodes = request.IncludeLineage && context.NodeRows is { Count: > 0 }
            ? [.. context.NodeRows.Select(x => (object)new { x.Category, x.Type, x.Title, x.Status })]
            : [];
        IReadOnlyList<object> lineageConnections = request.IncludeLineage && context.ConnectionRows is { Count: > 0 }
            ? [.. context.ConnectionRows.Select(x => (object)new { x.FromNode, x.FromPin, x.ToNode, x.ToPin, x.DataType })]
            : [];

        bool showSql = request.IncludeSql;
        bool showMetadata = metadataRows.Count > 0;
        bool showLineage = lineageNodes.Count > 0 || lineageConnections.Count > 0;

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine($"<html lang=\"{Html(language)}\" data-theme=\"dark\">");
        html.AppendLine("<head><meta charset=\"UTF-8\"/><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"/>");
        html.AppendLine($"<title>{Html(title)} - AkkornStudio Report</title><style>{BuildStyles()}</style></head><body>");
        html.AppendLine("<header><div class=\"brand\">AkkornStudio</div><div class=\"grow\">");
        html.AppendLine($"<h1>{Html(title)}</h1>");
        if (!string.IsNullOrWhiteSpace(description))
            html.AppendLine($"<div class=\"description\">{Html(description)}</div>");
        html.AppendLine("</div><div class=\"toolbar\">");
        html.AppendLine($"<button id=\"themeBtn\" class=\"with-icon\" type=\"button\">{IconSvg("moon")}<span data-label>{Html(Txt(language, "Tema", "Theme"))}</span></button>");
        html.AppendLine($"<button id=\"summaryBtn\" class=\"with-icon\" type=\"button\">{IconSvg("clipboard")}<span data-label>{Html(Txt(language, "Copiar resumo", "Copy summary"))}</span></button>");
        html.AppendLine($"<button id=\"sqlBtn\" class=\"with-icon\" type=\"button\"{(showSql ? string.Empty : " style=\"display:none\"")}>{IconSvg("code")}<span data-label>{Html(Txt(language, "Copiar SQL", "Copy SQL"))}</span></button>");
        html.AppendLine($"<button id=\"csvBtn\" class=\"with-icon\" type=\"button\">{IconSvg("download")}<span data-label>{Html(Txt(language, "Exportar visivel CSV", "Export visible CSV"))}</span></button>");
        html.AppendLine($"<button id=\"printBtn\" class=\"with-icon\" type=\"button\">{IconSvg("printer")}<span data-label>Print / PDF</span></button></div></header><main>");
        html.AppendLine(BuildOverviewSection(language));
        html.AppendLine(BuildDataSection(language, "results", Txt(language, "Resultados", "Results"), Txt(language, "Buscar resultados", "Search results"), false));
        html.AppendLine(BuildDataSection(language, "schema", Txt(language, "Colunas e schema", "Columns and Schema"), Txt(language, "Buscar schema", "Search schema"), true));
        if (showSql)
            html.AppendLine(BuildSqlSection(language));
        if (showMetadata)
            html.AppendLine(BuildDataSection(language, "metadata", Txt(language, "Metadados", "Metadata"), Txt(language, "Buscar metadados", "Search metadata"), true));
        if (showLineage)
        {
            html.AppendLine(BuildDataSection(language, "lineageNodes", Txt(language, "Nos de linhagem", "Lineage nodes"), Txt(language, "Buscar nos", "Search nodes"), true));
            html.AppendLine(BuildDataSection(language, "lineageConnections", Txt(language, "Conexoes de linhagem", "Lineage connections"), Txt(language, "Buscar conexoes", "Search connections"), true));
        }
        html.AppendLine(BuildCellModal(language));
        html.AppendLine("</main><footer id=\"footer\"></footer><script>");
        html.AppendLine($"const META={Json(meta)};const ROWS={Json(context.ResultRows)};const SCHEMA={Json(schemaRows)};const MROWS={Json(metadataRows)};const LINEAGE_NODES={Json(lineageNodes)};const LINEAGE_CONNECTIONS={Json(lineageConnections)};const SQL={Json(showSql ? context.Sql : string.Empty)};const LABELS={Json(BuildLabels(language))};");
        html.AppendLine($"</script><script>{BuildScript().Replace("__REPORT_VERSION__", ReportVersion, StringComparison.Ordinal)}</script></body></html>");
        return html.ToString();
    }

    private static string BuildOverviewSection(string language)
        => $"<section id=\"overview\"><div class=\"head\"><h2>{Html(Txt(language, "Visao geral", "Overview"))}</h2><div class=\"controls\"><button type=\"button\" data-collapse=\"overview\">{Html(Txt(language, "Recolher", "Collapse"))}</button></div></div><div class=\"body\" id=\"overviewBody\"></div></section>";

    private static string BuildDataSection(string language, string id, string title, string searchPlaceholder, bool collapsed)
    {
        string collapseClass = collapsed ? "collapsed" : string.Empty;
        string collapseLabel = collapsed ? Txt(language, "Expandir", "Expand") : Txt(language, "Recolher", "Collapse");
        return $"""
<section id="{id}" class="{collapseClass}">
  <div class="head">
    <h2>{Html(title)}</h2>
    <div class="controls">
      <input id="{id}Search" class="input compact-search" type="search" placeholder="{Html(searchPlaceholder)}"/>
      <button id="{id}OpenFilters" class="with-icon" type="button">{IconSvg("filter")}<span data-label>{Html(Txt(language, "Filtros", "Filters"))}</span></button>
      <button id="{id}OpenSort" class="with-icon" type="button">{IconSvg("sort")}<span data-label>{Html(Txt(language, "Ordenacao", "Sorting"))}</span></button>
      <button id="{id}OpenColumns" class="with-icon" type="button">{IconSvg("columns")}<span data-label>{Html(Txt(language, "Colunas", "Columns"))}</span></button>
      <button id="{id}Reset" class="with-icon" type="button">{IconSvg("rotate")}<span data-label>{Html(Txt(language, "Resetar visao", "Reset view"))}</span></button>
      <button type="button" data-collapse="{id}">{Html(collapseLabel)}</button>
    </div>
  </div>
  <div class="body">
    <div class="panel-strip">
      <div class="panel-card">
        <div class="panel-title">{Html(Txt(language, "Filtros ativos", "Active filters"))}</div>
        <div class="panel-actions">
          <button id="{id}InlineFiltersToggle" class="with-icon" type="button">{IconSvg("chevron-down")}<span data-label>{Html(Txt(language, "Expandir", "Expand"))}</span></button>
          <button id="{id}Clear" class="with-icon" type="button">{IconSvg("eraser")}<span data-label>{Html(Txt(language, "Limpar", "Clear"))}</span></button>
        </div>
      </div>
      <div id="{id}FiltersWrap" class="inline-panel collapsed">
        <div id="{id}Count" class="tiny-stat">0</div>
        <div id="{id}Filters" class="chips chips-wrap"></div>
      </div>
      <div class="panel-card">
        <div class="panel-title">{Html(Txt(language, "Colunas visiveis", "Visible columns"))}</div>
        <div class="panel-actions">
          <button id="{id}ColsInlineToggle" class="with-icon" type="button">{IconSvg("chevron-down")}<span data-label>{Html(Txt(language, "Expandir", "Expand"))}</span></button>
          <span id="{id}ColsCount" class="tiny-stat">0</span>
          <button id="{id}ColsReset" class="with-icon" type="button">{IconSvg("rotate")}<span data-label>{Html(Txt(language, "Resetar", "Reset"))}</span></button>
        </div>
      </div>
      <div id="{id}ColsWrap" class="inline-panel collapsed">
        <div id="{id}ColsSummary" class="chips chips-wrap summary-chips"></div>
      </div>
      <div class="panel-card">
        <div class="panel-title">{Html(Txt(language, "Ordenacao ativa", "Active sorting"))}</div>
        <div class="panel-actions">
          <button id="{id}SortReset" class="with-icon" type="button">{IconSvg("rotate")}<span data-label>{Html(Txt(language, "Resetar", "Reset"))}</span></button>
        </div>
      </div>
      <div id="{id}SortSummary" class="chips chips-wrap summary-chips"></div>
    </div>
    <div id="{id}Table"></div>
  </div>
</section>
"""+ BuildFilterModal(language, id) + BuildSortModal(language, id) + BuildColumnsModal(language, id);
    }

    private static string BuildFilterModal(string language, string id) => $"""
<div id="{id}FilterModal" class="modal-shell" aria-hidden="true">
  <div class="modal-backdrop" data-close-modal="{id}FilterModal"></div>
  <div class="modal">
    <div class="modal-head">
      <div><div class="modal-title">{Html(Txt(language, "Adicionar filtro", "Add filter"))}</div><div class="muted">{Html(Txt(language, "Escolha coluna, condicao e valor.", "Choose column, condition and value."))}</div></div>
      <button type="button" class="icon-btn" data-close-modal="{id}FilterModal">{IconSvg("x")}</button>
    </div>
    <div class="modal-body">
      <div class="form-grid">
        <div class="field-stack"><label class="field-label with-icon" for="{id}FilterCol">{IconSvg("columns")}{Html(Txt(language, "Coluna", "Column"))}</label><select id="{id}FilterCol" class="input"></select></div>
        <div class="field-stack"><label class="field-label with-icon" for="{id}Op">{IconSvg("funnel")}{Html(Txt(language, "Condicao", "Condition"))}</label><select id="{id}Op" class="input"></select></div>
        <div class="field-span field-stack"><label class="field-label with-icon">{IconSvg("text")}{Html(Txt(language, "Valor", "Value"))}</label><div id="{id}ValueWrap" class="value-wrap"></div></div>
      </div>
      <div class="modal-note">{Html(Txt(language, "Contains procura trechos; Like aceita % e _; Regex usa expressoes regulares do JavaScript.", "Contains searches fragments; Like accepts % and _; Regex uses JavaScript regular expressions."))}</div>
      <div id="{id}FiltersPreview" class="chips chips-wrap"></div>
    </div>
    <div class="modal-foot">
      <button id="{id}Add" class="with-icon" type="button">{IconSvg("plus")}<span data-label>{Html(Txt(language, "Adicionar filtro", "Add filter"))}</span></button>
      <button type="button" data-close-modal="{id}FilterModal">{Html(Txt(language, "Fechar", "Close"))}</button>
    </div>
  </div>
</div>
""";

    private static string BuildSortModal(string language, string id) => $"""
<div id="{id}SortModal" class="modal-shell" aria-hidden="true">
  <div class="modal-backdrop" data-close-modal="{id}SortModal"></div>
  <div class="modal">
    <div class="modal-head">
      <div><div class="modal-title">{Html(Txt(language, "Gerenciar ordenacao", "Manage sorting"))}</div><div class="muted">{Html(Txt(language, "Defina coluna e direcao sem depender do cabecalho.", "Choose column and direction without relying on the header."))}</div></div>
      <button type="button" class="icon-btn" data-close-modal="{id}SortModal">{IconSvg("x")}</button>
    </div>
    <div class="modal-body">
      <div class="form-grid">
        <div class="field-stack"><label class="field-label with-icon" for="{id}SortCol">{IconSvg("sort")} {Html(Txt(language, "Coluna de ordenacao", "Sort column"))}</label><select id="{id}SortCol" class="input"></select></div>
        <div class="field-stack"><label class="field-label with-icon" for="{id}SortDir">{IconSvg("arrow-up-down")} {Html(Txt(language, "Direcao", "Direction"))}</label><select id="{id}SortDir" class="input"><option value="asc">{Html(Txt(language, "Crescente", "Ascending"))}</option><option value="desc">{Html(Txt(language, "Decrescente", "Descending"))}</option></select></div>
      </div>
      <div id="{id}SortPreview" class="chips chips-wrap" style="margin-top:12px"></div>
    </div>
    <div class="modal-foot">
      <button id="{id}SortApply" class="with-icon" type="button">{IconSvg("check")}<span data-label>{Html(Txt(language, "Aplicar ordenacao", "Apply sorting"))}</span></button>
      <button id="{id}SortClear" class="with-icon" type="button">{IconSvg("rotate")}<span data-label>{Html(Txt(language, "Resetar", "Reset"))}</span></button>
    </div>
  </div>
</div>
""";

    private static string BuildColumnsModal(string language, string id) => $"""
<div id="{id}ColumnsModal" class="modal-shell" aria-hidden="true">
  <div class="modal-backdrop" data-close-modal="{id}ColumnsModal"></div>
  <div class="modal modal-wide">
    <div class="modal-head">
      <div><div class="modal-title">{Html(Txt(language, "Colunas visiveis", "Visible columns"))}</div><div class="muted">{Html(Txt(language, "Escolha o que permanece na tabela.", "Choose what stays in the table."))}</div></div>
      <button type="button" class="icon-btn" data-close-modal="{id}ColumnsModal">{IconSvg("x")}</button>
    </div>
    <div class="modal-body">
      <div class="picklist">
        <div class="pick-pane"><label class="field-label" for="{id}ColsSearch">{Html(Txt(language, "Disponiveis", "Available"))}</label><input id="{id}ColsSearch" class="input" type="search" placeholder="{Html(Txt(language, "Buscar colunas", "Search columns"))}"/><div id="{id}AvailableCols" class="pick-list"></div></div>
        <div class="pick-actions"><button id="{id}ColsAdd" type="button">&gt;</button><button id="{id}ColsRemove" type="button">&lt;</button><button id="{id}ColsAll" type="button">&gt;&gt;</button><button id="{id}ColsNone" type="button">&lt;&lt;</button></div>
        <div class="pick-pane"><div class="field-label">{Html(Txt(language, "Em exibicao", "Showing"))}</div><div id="{id}VisibleCols" class="pick-list"></div></div>
      </div>
    </div>
    <div class="modal-foot"><button id="{id}ColsDone" class="with-icon" type="button">{IconSvg("check")}<span data-label>{Html(Txt(language, "Concluir", "Done"))}</span></button></div>
  </div>
</div>
""";

    private static string BuildSqlSection(string language)
        => $"<section id=\"sql\" class=\"collapsed\"><div class=\"head\"><h2>SQL</h2><div class=\"controls\"><button type=\"button\" data-collapse=\"sql\">{Html(Txt(language, "Expandir", "Expand"))}</button></div></div><div class=\"body\"><div class=\"sql\"><pre id=\"sqlPre\"></pre></div></div></section>";

    private static string BuildCellModal(string language) => $"""
<div id="cellModal" class="modal-shell" aria-hidden="true">
  <div class="modal-backdrop" data-close-modal="cellModal"></div>
  <div class="modal modal-cell">
    <div class="modal-head">
      <div><div class="modal-title">{Html(Txt(language, "Conteudo completo", "Full content"))}</div><div class="muted">{Html(Txt(language, "Visualizacao ampliada da celula selecionada.", "Expanded view for the selected cell."))}</div></div>
      <button type="button" class="icon-btn" data-close-modal="cellModal">{IconSvg("x")}</button>
    </div>
    <div class="modal-body"><pre id="cellModalText" class="cell-modal-text"></pre></div>
  </div>
</div>
""";

    private static object BuildMeta(SqlEditorReportExportContext context, SqlEditorReportExportRequest request, string generatedAt, string generatedAtIso, string title, string description)
    {
        return new
        {
            title,
            description,
            generatedAt,
            generatedAtIso,
            status = NormalizeStatus(context.ExecutionResult.Status),
            rowCount = context.ExecutionResult.RowCount,
            executionTimeMs = context.ExecutionResult.ExecutionTimeMs,
            columnCount = context.SchemaColumns.Count,
            joinCount = CountMatches(context.Sql, @"\bjoin\b"),
            aggregateCount = CountMatches(context.Sql, @"\b(count|sum|avg|min|max|string_agg|group_concat)\s*\("),
            hasSubquery = ContainsSubquery(context.Sql),
            emptyValueMode = EmptyValueMode(request.EmptyValueDisplayMode),
            statusMessage = context.ExecutionResult.ErrorMessage,
            filePath = context.ActiveFilePath,
            metadataLevel = request.MetadataLevel.ToString(),
        };
    }

    private static IReadOnlyList<SqlEditorReportSchemaDetail> BuildSchemaRows(SqlEditorReportExportContext context)
    {
        if (context.SchemaDetails is { Count: > 0 })
            return context.SchemaDetails;

        IReadOnlyList<string> columns = context.SchemaColumns.Count > 0 ? context.SchemaColumns : [.. context.ResultRows.FirstOrDefault()?.Keys ?? []];
        var details = new List<SqlEditorReportSchemaDetail>(columns.Count);
        foreach (string column in columns)
        {
            long nullCount = 0;
            var distinct = new HashSet<string>(StringComparer.Ordinal);
            string kind = "text";
            string? example = null;
            string? minValue = null;
            string? maxValue = null;
            foreach (IReadOnlyDictionary<string, object?> row in context.ResultRows)
            {
                row.TryGetValue(column, out object? value);
                if (value is null)
                {
                    nullCount += 1;
                    continue;
                }

                string text = value.ToString() ?? string.Empty;
                example ??= text;
                distinct.Add(text);
                minValue = minValue is null || string.CompareOrdinal(text, minValue) < 0 ? text : minValue;
                maxValue = maxValue is null || string.CompareOrdinal(text, maxValue) > 0 ? text : maxValue;
                kind = MergeKinds(kind, DetectKind(value));
            }

            details.Add(new SqlEditorReportSchemaDetail(column, kind, nullCount, distinct.Count, example, minValue, maxValue));
        }

        return details;
    }

    private static IReadOnlyList<object> BuildMetadataRows(SqlEditorReportExportContext context, SqlEditorReportExportRequest request, string generatedAtIso, string language)
    {
        if (request.MetadataLevel == SqlEditorReportMetadataLevel.None)
            return [];

        ConnectionConfig? connection = context.Connection;
        var rows = new List<object>
        {
            new { field = "provider", label = Txt(language, "Provedor", "Provider"), value = connection?.Provider.ToString(), kind = "text" },
            new { field = "database", label = Txt(language, "Banco", "Database"), value = connection?.Database, kind = "text" },
            new { field = "host", label = "Host", value = connection?.Host, kind = "text" },
            new { field = "executionDate", label = Txt(language, "Data de execucao", "Execution date"), value = generatedAtIso, kind = "date" },
            new { field = "locale", label = "Locale", value = CultureInfo.CurrentCulture.Name, kind = "text" },
            new { field = "timezone", label = "Timezone", value = TimeZoneInfo.Local.Id, kind = "text" },
        };

        if (request.MetadataLevel == SqlEditorReportMetadataLevel.Complete)
        {
            rows.Add(new { field = "dialect", label = "Dialect", value = InferDialect(connection?.Provider), kind = "text" });
            rows.Add(new { field = "filePath", label = Txt(language, "Arquivo", "File path"), value = context.ActiveFilePath, kind = "text" });
            rows.Add(new { field = "rowCount", label = Txt(language, "Linhas", "Rows"), value = context.ExecutionResult.RowCount, kind = "number" });
            rows.Add(new { field = "executionTimeMs", label = Txt(language, "Tempo (ms)", "Execution time (ms)"), value = context.ExecutionResult.ExecutionTimeMs, kind = "number" });
        }

        return rows;
    }

    private static string BuildStyles() => string.Concat(
        "*{box-sizing:border-box}body{margin:0;font-family:'Segoe UI',system-ui,sans-serif;background:var(--bg);color:var(--text)}",
        ":root[data-theme=dark]{--bg:", UiColorConstants.C_090B14.ToLowerInvariant(), ";--panel:", UiColorConstants.C_12172A.ToLowerInvariant(), ";--surface:", UiColorConstants.C_1E2A3F.ToLowerInvariant(), ";--surface2:", UiColorConstants.C_252C3F.ToLowerInvariant(), ";--border:", UiColorConstants.C_2A3554.ToLowerInvariant(), ";--border2:", UiColorConstants.C_334164.ToLowerInvariant(), ";--text:", UiColorConstants.C_E7ECFF.ToLowerInvariant(), ";--muted:", UiColorConstants.C_AEB9D9.ToLowerInvariant(), ";--accent:", UiColorConstants.C_5B7CFA.ToLowerInvariant(), ";--ok:", UiColorConstants.C_2FBF84.ToLowerInvariant(), ";--warn:", UiColorConstants.C_D9A441.ToLowerInvariant(), ";--err:", UiColorConstants.C_E16174.ToLowerInvariant(), ";--code:", UiColorConstants.C_0F1220.ToLowerInvariant(), ";--overlay:rgba(7,10,18,.72)}",
        ":root[data-theme=light]{--bg:", UiColorConstants.C_F0F2FA.ToLowerInvariant(), ";--panel:", UiColorConstants.C_FFFFFF.ToLowerInvariant(), ";--surface:", UiColorConstants.C_F7F8FD.ToLowerInvariant(), ";--surface2:", UiColorConstants.C_EAECF5.ToLowerInvariant(), ";--border:", UiColorConstants.C_D0D4E8.ToLowerInvariant(), ";--border2:", UiColorConstants.C_B8BDD6.ToLowerInvariant(), ";--text:", UiColorConstants.C_1A1D2E.ToLowerInvariant(), ";--muted:", UiColorConstants.C_4A4F6A.ToLowerInvariant(), ";--accent:", UiColorConstants.C_5B7CFA.ToLowerInvariant(), ";--ok:", UiColorConstants.C_2FBF84.ToLowerInvariant(), ";--warn:", UiColorConstants.C_D9A441.ToLowerInvariant(), ";--err:", UiColorConstants.C_E16174.ToLowerInvariant(), ";--code:", UiColorConstants.C_F7F8FD.ToLowerInvariant(), ";--overlay:rgba(90,100,130,.26)}",
        "header{display:flex;flex-wrap:wrap;gap:16px;align-items:flex-start;padding:18px 20px;background:var(--panel);border-bottom:1px solid var(--border)}main{max-width:1480px;margin:0 auto;padding:18px 14px 28px;display:grid;gap:14px}.brand{font-size:12px;letter-spacing:.16em;text-transform:uppercase;color:var(--accent);font-weight:800}.grow{flex:1 1 920px;min-width:520px}h1{margin:0;font-size:20px}.description{margin-top:10px;color:var(--muted);font-size:13px;line-height:1.65;width:100%;max-width:none;min-height:104px;padding:16px 18px;border:1px solid var(--border);border-radius:14px;background:var(--surface)}.toolbar,.controls,.chips,.chips-wrap,.panel-actions,.pager,.pager-main,.form-grid,.pick-actions{display:flex;gap:8px;align-items:center}.toolbar,.controls,.chips,.chips-wrap,.form-grid,.pager{flex-wrap:wrap}.with-icon{display:inline-flex;align-items:center;gap:8px}.icon-only{padding:8px;min-width:38px;justify-content:center}.icon{display:inline-flex;align-items:center;justify-content:center;width:16px;height:16px;flex:0 0 auto}.icon svg{width:16px;height:16px;stroke:currentColor;fill:none;stroke-width:2;stroke-linecap:round;stroke-linejoin:round}section{background:var(--panel);border:1px solid var(--border2);border-radius:18px;overflow:hidden}.head{display:flex;justify-content:space-between;gap:12px;align-items:center;padding:14px 16px;border-bottom:1px solid var(--border)}.head h2{margin:0;font-size:13px;text-transform:uppercase;letter-spacing:.12em;color:var(--muted)}.body{padding:14px}.collapsed .body{display:none}.grid{display:grid;gap:10px;grid-template-columns:repeat(auto-fit,minmax(160px,1fr))}.card{background:var(--surface);border:1px solid var(--border);border-radius:14px;padding:14px;min-height:96px}.card.wide{grid-column:1 / -1;min-height:132px}.label,th,.field-label{font-size:10px;letter-spacing:.12em;text-transform:uppercase;color:var(--muted)}.field-stack{display:flex;flex-direction:column;gap:6px}.value{margin-top:6px;font-size:18px;font-weight:800;word-break:break-word}.value.small{font-size:14px;line-height:1.65}.banner{margin-top:12px;padding:12px 14px;border-radius:14px;border:1px solid var(--border);background:var(--surface)}.banner.ok{border-color:var(--ok)}.banner.warn{border-color:var(--warn)}.banner.err{border-color:var(--err)}button,input,select{font:inherit}button{border:1px solid var(--border);background:var(--surface);color:var(--text);border-radius:999px;padding:8px 12px;cursor:pointer}button:hover{border-color:var(--accent)}button:disabled{cursor:not-allowed;opacity:.46;border-color:var(--border);color:var(--muted);background:var(--surface2)}button:disabled:hover{border-color:var(--border)}button.active{background:var(--accent);border-color:var(--accent);color:#fff}.icon-btn{width:34px;height:34px;padding:0;border-radius:999px}.input{min-width:150px;border-radius:12px;border:1px solid var(--border);background:var(--panel);color:var(--text);padding:9px 11px}.compact-search{min-width:220px}.field-span{flex:1 1 100%}.value-wrap{display:flex;flex:1 1 auto}.value-wrap .input{width:100%}.panel-strip{display:grid;gap:10px;margin-bottom:12px}.panel-card,.inline-panel{background:var(--surface);border:1px solid var(--border);border-radius:14px;padding:12px}.panel-card{display:flex;justify-content:space-between;gap:10px;align-items:center}.panel-title{font-size:12px;font-weight:700}.inline-panel.collapsed{display:none}.tiny-stat{display:inline-flex;align-items:center;justify-content:center;min-width:28px;height:28px;padding:0 10px;border-radius:999px;background:var(--surface2);font-weight:700}.summary-chips{margin-top:8px}.chip{display:inline-flex;align-items:center;gap:8px;border-radius:999px;padding:6px 10px;border:1px solid var(--accent);background:var(--surface2)}.chip button{padding:3px 7px;background:transparent}.muted{color:var(--muted);font-size:12px}.modal-note{margin-top:10px;color:var(--muted);font-size:12px;line-height:1.5}.empty{padding:18px;border:1px dashed var(--border);border-radius:12px;color:var(--muted)}pre{margin:0;font-family:'JetBrains Mono','Cascadia Code',Consolas,monospace;white-space:pre-wrap;line-height:1.65}.sql{background:var(--code);border:1px solid var(--border);border-radius:14px;padding:16px;overflow:auto}.tbl{overflow:auto;border:1px solid var(--border);border-radius:14px;background:var(--surface)}table{width:max-content;min-width:100%;border-collapse:collapse}th,td{padding:10px;border-bottom:1px solid var(--border);text-align:left;vertical-align:top}th{cursor:pointer;background:var(--surface2);white-space:nowrap}td{min-width:120px;max-width:360px;user-select:text}.row-select-head,.row-select-cell{min-width:42px;width:42px;padding:0 6px;text-align:center;background:var(--surface2)}.row-select-cell{background:var(--surface)}.row-select-btn{width:22px;height:22px;padding:0;border-radius:999px;border:1px solid var(--border2)}tr.selected-row td{background:color-mix(in srgb,var(--accent) 10%,var(--surface))}td.selected-cell{outline:2px solid var(--accent);outline-offset:-2px;background:color-mix(in srgb,var(--accent) 12%,var(--surface))}.cell-content{display:flex;align-items:flex-start;gap:8px}.cell-text{display:block;white-space:pre-wrap;word-break:break-word}.cell-text.truncated{display:-webkit-box;-webkit-line-clamp:3;-webkit-box-orient:vertical;overflow:hidden;max-height:4.3em}.cell-text.type-text{color:var(--text)}.cell-text.type-number{color:#7dd3fc}.cell-text.type-date{color:#f9a8d4}.cell-text.type-bool{color:#facc15}.cell-text.type-null{color:var(--muted);font-style:italic}.cell-expand{padding:4px 8px;border-radius:999px;font-size:11px;white-space:nowrap}.th-sorted-asc::after{content:' ↑';color:var(--accent)}.th-sorted-desc::after{content:' ↓';color:var(--accent)}.pager{justify-content:space-between;gap:12px;padding:12px 2px 0}.pager-main{flex-wrap:wrap;gap:10px;padding:8px 10px;border:1px solid var(--border);border-radius:999px;background:var(--surface2)}.pager-status{display:inline-flex;align-items:center;padding:0 4px;font-size:12px;font-weight:700;color:var(--muted)}.pager-chip{display:inline-flex;align-items:center;justify-content:center;min-width:36px;height:36px;border-radius:999px;background:var(--surface);border:1px solid var(--border)}.page-input{width:82px;min-width:82px;text-align:center}.page-size{min-width:110px}.picklist{display:grid;grid-template-columns:minmax(0,1fr) auto minmax(0,1fr);gap:12px;align-items:stretch}.pick-pane{display:flex;flex-direction:column;gap:8px}.pick-list{display:flex;flex-direction:column;gap:6px;min-height:240px;max-height:320px;overflow:auto;padding:10px;border:1px solid var(--border);border-radius:12px;background:var(--panel)}.pick-item{display:flex;align-items:center;gap:8px;padding:9px 10px;border-radius:10px;border:1px solid transparent;background:transparent;color:var(--text);cursor:pointer;text-align:left}.pick-item:hover{background:var(--surface2)}.pick-item.selected{border-color:var(--accent);background:color-mix(in srgb,var(--accent) 18%,transparent)}.pick-actions{flex-direction:column;justify-content:center}.pick-actions button{min-width:42px}.modal-shell{position:fixed;inset:0;display:none;align-items:center;justify-content:center;z-index:50}.modal-shell.open{display:flex}.modal-backdrop{position:absolute;inset:0;background:var(--overlay)}.modal{position:relative;z-index:1;width:min(760px,calc(100vw - 24px));max-height:calc(100vh - 40px);display:flex;flex-direction:column;background:var(--panel);border:1px solid var(--border2);border-radius:20px;box-shadow:0 24px 64px rgba(0,0,0,.28)}.modal-wide{width:min(980px,calc(100vw - 24px))}.modal-cell{width:min(900px,calc(100vw - 24px));height:min(85vh,780px)}.modal-head,.modal-foot{display:flex;justify-content:space-between;gap:12px;align-items:center;padding:16px;border-bottom:1px solid var(--border)}.modal-foot{border-bottom:none;border-top:1px solid var(--border);justify-content:flex-end}.modal-title{font-size:16px;font-weight:700}.modal-body{padding:16px;overflow:auto;min-height:0}.modal-cell .modal-body{flex:1 1 auto;overflow:auto}.cell-modal-text{padding:14px;border:1px solid var(--border);border-radius:14px;background:var(--surface);white-space:pre-wrap;word-break:break-word;overflow-wrap:anywhere;overflow:auto;max-width:100%;min-height:100%;max-height:none}.cell-modal-text code{white-space:pre-wrap;word-break:break-word;overflow-wrap:anywhere}footer{padding:16px 20px 22px;color:var(--muted);text-align:center}@media (max-width:900px){main{padding:14px 10px 24px}.grow{min-width:0}.description{min-height:104px;padding:14px 16px}.head,.panel-card,.modal-head,.modal-foot,.pager{align-items:flex-start;flex-direction:column}.picklist{grid-template-columns:1fr}.pick-actions{flex-direction:row;justify-content:flex-start}.modal-cell{height:min(88vh,calc(100vh - 24px))}.card.wide{grid-column:auto}}@media print{header .toolbar,button,.modal-shell{display:none!important}body{background:#fff;color:#111}main{max-width:none;padding:0}section{break-inside:avoid}.tbl{overflow:visible}}"
    );

    private static string BuildScript() =>
"""
(() => {
  const storeKey='akkorn-report-view-v9',$=id=>document.getElementById(id),txt=(k,f)=>LABELS[k]||f;
  const esc=v=>{const d=document.createElement('div');d.textContent=v??'';return d.innerHTML;};
  const read=()=>{try{return JSON.parse(localStorage.getItem(storeKey)||'{}')}catch{return{}}};
  const saved=read(),state=saved.state||{},collapsed=saved.collapsed||{};let theme=saved.theme||document.documentElement.getAttribute('data-theme')||'dark';
  const detect=(k,rows)=>{for(const row of rows){const v=row[k];if(v===null||v===undefined||v==='')continue;if(typeof v==='number')return'number';if(typeof v==='boolean')return'bool';if(String(v).match(/^\\d{4}-\\d{2}-\\d{2}(?:[T\\s].*)?$/))return'date';return'text'}return'text'};
  const schemaName=row=>row?.Name||row?.name||row?.Column||row?.column||'';
  const resultColumns=()=>{const keys=SCHEMA.length?SCHEMA.map(r=>schemaName(r)).filter(Boolean):Object.keys(ROWS[0]||{});return keys.map(key=>{const row=SCHEMA.find(r=>schemaName(r)===key);return{key,label:key,kind:row?(row.Kind||row.kind||'text'):detect(key,ROWS)}})};
  const schemaColumns=()=>[{key:'Name',label:txt('field_column','Column'),kind:'text'},{key:'Kind',label:txt('field_type','Type'),kind:'text'},{key:'NullCount',label:txt('field_nulls','Nulls'),kind:'number'},{key:'DistinctCount',label:txt('field_distinct','Distinct'),kind:'number'},{key:'Example',label:txt('field_example','Example'),kind:'text'},{key:'MinValue',label:txt('field_min','Min'),kind:'text'},{key:'MaxValue',label:txt('field_max','Max'),kind:'text'}];
  const metadataColumns=()=>[{key:'field',label:txt('field_field','Field'),kind:'text'},{key:'label',label:txt('field_label','Label'),kind:'text'},{key:'value',label:txt('field_value','Value'),kind:'text'}];
  const lineageNodeColumns=()=>[{key:'Category',label:txt('field_category','Category'),kind:'text'},{key:'Type',label:txt('field_type','Type'),kind:'text'},{key:'Title',label:txt('field_title','Title'),kind:'text'},{key:'Status',label:txt('field_status','Status'),kind:'text'}];
  const lineageConnectionColumns=()=>[{key:'FromNode',label:txt('field_from_node','From node'),kind:'text'},{key:'FromPin',label:txt('field_from_pin','From pin'),kind:'text'},{key:'ToNode',label:txt('field_to_node','To node'),kind:'text'},{key:'ToPin',label:txt('field_to_pin','To pin'),kind:'text'},{key:'DataType',label:txt('field_data_type','Data type'),kind:'text'}];
  const datasets={results:{rows:ROWS,columns:()=>resultColumns(),defaultSort:(resultColumns()[0]||{}).key||''},schema:{rows:SCHEMA,columns:()=>schemaColumns(),defaultSort:'column'},metadata:{rows:MROWS,columns:()=>metadataColumns(),defaultSort:'label'},lineageNodes:{rows:LINEAGE_NODES,columns:()=>lineageNodeColumns(),defaultSort:'Title'},lineageConnections:{rows:LINEAGE_CONNECTIONS,columns:()=>lineageConnectionColumns(),defaultSort:'FromNode'}};
  const display=v=>v===null||v===undefined||v===''?(META.emptyValueMode==='dash'?'-':META.emptyValueMode==='null'?'null':''):typeof v==='object'?JSON.stringify(v):String(v);
  const toPositiveInt=(value,fallback)=>{const parsed=parseInt(String(value??''),10);return Number.isFinite(parsed)&&parsed>0?parsed:fallback;};
  const getState=(name,sort)=>{const defaults={page:1,size:10,search:'',sortKey:sort||((datasets[name]?.columns()[0]||{}).key||''),sortDir:'asc',filters:[],visibleOrder:(datasets[name]?.columns()||[]).map(c=>c.key),availableSelection:[],visibleSelection:[],colSearch:'',filtersExpanded:false,columnsExpanded:false,selectedRows:[],selectedCell:null};const current=state[name]||{};state[name]={...defaults,...current};if(!Array.isArray(state[name].filters))state[name].filters=[];if(!Array.isArray(state[name].visibleOrder))state[name].visibleOrder=defaults.visibleOrder.slice();if(!Array.isArray(state[name].availableSelection))state[name].availableSelection=[];if(!Array.isArray(state[name].visibleSelection))state[name].visibleSelection=[];if(!Array.isArray(state[name].selectedRows))state[name].selectedRows=[];state[name].page=toPositiveInt(state[name].page,1);state[name].size=toPositiveInt(state[name].size,10);if(typeof state[name].filtersExpanded!=='boolean')state[name].filtersExpanded=false;if(typeof state[name].columnsExpanded!=='boolean')state[name].columnsExpanded=false;if(state[name].selectedCell&&typeof state[name].selectedCell!=='object')state[name].selectedCell=null;return state[name];};
  const save=()=>localStorage.setItem(storeKey,JSON.stringify({state,collapsed,theme}));
  const rowValue=(row,key)=>{if(!row||typeof row!=='object')return undefined;if(Object.prototype.hasOwnProperty.call(row,key))return row[key];const match=Object.keys(row).find(k=>String(k).toLowerCase()===String(key).toLowerCase());return match?row[match]:undefined;};
  const cmp=(v,k)=>v===null||v===undefined||v===''?null:k==='number'?(Number.isNaN(Number(v))?null:Number(v)):k==='bool'?String(v).toLowerCase()==='true':k==='date'?(Number.isNaN(Date.parse(String(v)))?null:Date.parse(String(v))):String(v).toLowerCase();
  const compare=(a,b,k)=>{const x=cmp(a,k),y=cmp(b,k);if(x===y)return 0;if(x===null)return 1;if(y===null)return-1;return x>y?1:-1};
  const ensureVisible=(name,columns)=>{const st=getState(name,datasets[name].defaultSort);if(!st.visibleOrder.length)st.visibleOrder=columns.map(c=>c.key);st.visibleOrder=st.visibleOrder.filter(key=>columns.some(c=>c.key===key));st.availableSelection=st.availableSelection.filter(key=>!st.visibleOrder.includes(key));st.visibleSelection=st.visibleSelection.filter(key=>st.visibleOrder.includes(key));};
  const setLabel=(id,value)=>{const button=$(id);const target=button?.querySelector('[data-label]');if(target)target.textContent=value;else if(button)button.textContent=value;};
  const setTheme=t=>{theme=t;document.documentElement.setAttribute('data-theme',theme);setLabel('themeBtn',theme==='dark'?txt('theme_light','Light'):txt('theme_dark','Dark'));save();};
  const openModal=id=>{$(id)?.classList.add('open');$(id)?.setAttribute('aria-hidden','false');},closeModal=id=>{$(id)?.classList.remove('open');$(id)?.setAttribute('aria-hidden','true');};
  const likeRegex=pattern=>new RegExp(`^${String(pattern).replace(/[.+^${}()|[\\]\\\\]/g,'\\\\$&').replace(/%/g,'.*').replace(/_/g,'.')}$`,'i');
  const ops=kind=>kind==='number'||kind==='date'?['=','!=','>','>=','<','<=']:kind==='bool'?['=','!=']:['contains','like','regex','=','!='];
  const matches=(value,filter,kind)=>{const raw=display(value),left=cmp(value,kind),right=cmp(filter.value,kind);if(filter.operator==='contains')return raw.toLowerCase().includes(String(filter.value).toLowerCase());if(filter.operator==='like'){try{return likeRegex(filter.value).test(raw)}catch{return false}}if(filter.operator==='regex'){try{return new RegExp(String(filter.value),'i').test(raw)}catch{return false}}if(filter.operator==='=')return left===right;if(filter.operator==='!=')return left!==right;if(left===null||right===null)return false;if(filter.operator==='>')return left>right;if(filter.operator==='>=')return left>=right;if(filter.operator==='<')return left<right;if(filter.operator==='<=')return left<=right;return false};
  const applyFilters=(name,rows,columns)=>{const st=getState(name,datasets[name].defaultSort);let out=rows.slice();if(!columns.length)return out;if(st.search){const term=st.search.toLowerCase();out=out.filter(row=>columns.some(col=>display(rowValue(row,col.key)).toLowerCase().includes(term)))}if(st.filters.length){out=out.filter(row=>st.filters.every(filter=>{const col=columns.find(item=>item.key===filter.column)||columns[0];return col?matches(rowValue(row,filter.column),filter,col.kind):true}))}return out};
  const renderValueInput=name=>{const dataset=datasets[name],col=$(`${name}FilterCol`),op=$(`${name}Op`),host=$(`${name}ValueWrap`);if(!dataset||!col||!op||!host)return;const columns=dataset.columns(),column=columns.find(item=>item.key===col.value)||columns[0];if(!column){op.innerHTML='';host.innerHTML=`<input id="${name}Value" class="input" type="search" disabled placeholder="${esc(txt('table_no_rows','No rows available.'))}"/>`;return}op.innerHTML=ops(column.kind).map(v=>`<option value="${esc(v)}">${esc(v)}</option>`).join('');if(column.kind==='bool'){host.innerHTML=`<select id="${name}Value" class="input"><option value="true">true</option><option value="false">false</option></select>`;return}if(column.kind==='date'){host.innerHTML=`<input id="${name}Value" class="input" type="date"/>`;return}if(column.kind==='number'){host.innerHTML=`<input id="${name}Value" class="input" type="number"/>`;return}const values=[...new Set(dataset.rows.map(row=>display(rowValue(row,column.key))).filter(Boolean))];host.innerHTML=values.length&&values.length<=20?`<input id="${name}Value" class="input" list="${name}ValueList" type="search" placeholder="${esc(txt('filter_value_placeholder','Filter value'))}"/><datalist id="${name}ValueList">${values.map(v=>`<option value="${esc(v)}"></option>`).join('')}</datalist>`:`<input id="${name}Value" class="input" type="search" placeholder="${esc(txt('filter_value_placeholder','Filter value'))}"/>`;};
  const card=(l,v,small,wide)=>`<div class="card${wide?' wide':''}"><div class="label">${esc(l)}</div><div class="value${small?' small':''}">${esc(v||'')}</div></div>`;
  const statusBanner=()=>`<div class="banner ${META.status==='error'?'err':META.status==='warning'?'warn':'ok'}">${esc(META.statusMessage||txt('overview_ok','Execution completed without additional messages.'))}</div>`;
  const renderOverview=()=>{const cards=[card(txt('summary_status','Status'),META.status),card(txt('summary_rows','Rows'),String(META.rowCount||0)),card(txt('summary_columns','Columns'),String(META.columnCount||0)),card(txt('summary_execution_time','Execution time'),`${META.executionTimeMs||0} ms`),card(txt('summary_joins','Joins'),String(META.joinCount||0)),card(txt('summary_aggregates','Aggregates'),String(META.aggregateCount||0)),card(txt('summary_subquery','Subquery'),META.hasSubquery?'true':'false'),card(txt('summary_query_length','Query length'),String((SQL||'').length)),card(txt('overview_generated_at','Generated'),META.generatedAt||'',true)];if(META.description)cards.push(card(txt('overview_description','Description'),META.description,true,true));if(META.filePath)cards.push(card(txt('overview_file_path','File path'),META.filePath,true,true));if($('overviewBody'))$('overviewBody').innerHTML=`<div class="grid">${cards.join('')}</div>${statusBanner()}`;};
  const renderFilterChips=name=>{const host=$(`${name}Filters`),preview=$(`${name}FiltersPreview`),count=$(`${name}Count`);if(!host||!preview||!count)return;const st=getState(name,datasets[name].defaultSort);count.textContent=String(st.filters.length);const body=st.filters.length?st.filters.map((f,i)=>`<div class="chip"><span>${esc(f.column)}</span><span>${esc(f.operator)}</span><span>${esc(f.value)}</span><button type="button" data-remove-filter="${name}:${i}">x</button></div>`).join(''):`<div class="muted">${esc(txt('filters_none_active','No filters active.'))}</div>`;host.innerHTML=body;preview.innerHTML=body;document.querySelectorAll(`[data-remove-filter^="${name}:"]`).forEach(btn=>btn.onclick=()=>{const index=parseInt((btn.getAttribute('data-remove-filter')||'').split(':')[1]||'0',10);st.filters.splice(index,1);st.page=1;renderDataset(name);});};
  const renderColumnsSummary=(name,columns)=>{const host=$(`${name}ColsSummary`),count=$(`${name}ColsCount`);if(!host||!count)return;const st=getState(name,datasets[name].defaultSort);const visible=(st.visibleOrder.length?st.visibleOrder:columns.map(c=>c.key)).map(key=>columns.find(c=>c.key===key)).filter(Boolean);count.textContent=String(visible.length);host.innerHTML=visible.length?visible.map(c=>`<span class="chip">${esc(c.label)}</span>`).join(''):`<div class="muted">${esc(txt('columns_none_showing','No visible columns.'))}</div>`;};
  const renderSortSummary=name=>{const host=$(`${name}SortSummary`),preview=$(`${name}SortPreview`);if(!host||!preview)return;const dataset=datasets[name],columns=dataset.columns(),st=getState(name,dataset.defaultSort),column=columns.find(col=>col.key===st.sortKey);const body=column?`<span class="chip">${esc(column.label)} · ${esc(st.sortDir==='asc'?txt('sort_asc','Ascending'):txt('sort_desc','Descending'))}</span>`:`<div class="muted">${esc(txt('sort_none','No active sorting.'))}</div>`;host.innerHTML=body;preview.innerHTML=body;};
  const pickItem=(column,selected)=>`<button type="button" class="pick-item${selected?' selected':''}" data-key="${esc(column.key)}">${esc(column.label)}</button>`;
  const toggle=(st,key,value,next)=>{if(!value)return;st[key]=st[key].includes(value)?st[key].filter(item=>item!==value):[...st[key],value];save();next();};
  const renderPickList=(name,columns)=>{const st=getState(name,datasets[name].defaultSort);ensureVisible(name,columns);const search=(st.colSearch||'').toLowerCase(),visibleSet=new Set(st.visibleOrder),available=columns.filter(c=>!visibleSet.has(c.key)&&c.label.toLowerCase().includes(search)),visible=(st.visibleOrder.length?st.visibleOrder:columns.map(c=>c.key)).map(key=>columns.find(c=>c.key===key)).filter(Boolean),availableHost=$(`${name}AvailableCols`),visibleHost=$(`${name}VisibleCols`);if(!availableHost||!visibleHost)return;availableHost.innerHTML=available.length?available.map(c=>pickItem(c,st.availableSelection.includes(c.key))).join(''):`<div class="muted">${esc(txt('columns_none_available','No columns available.'))}</div>`;visibleHost.innerHTML=visible.length?visible.map(c=>pickItem(c,st.visibleSelection.includes(c.key))).join(''):`<div class="muted">${esc(txt('columns_none_showing','No visible columns.'))}</div>`;availableHost.querySelectorAll('.pick-item').forEach(btn=>btn.onclick=()=>toggle(st,'availableSelection',btn.getAttribute('data-key'),()=>renderPickList(name,columns)));visibleHost.querySelectorAll('.pick-item').forEach(btn=>btn.onclick=()=>toggle(st,'visibleSelection',btn.getAttribute('data-key'),()=>renderPickList(name,columns)));};
  const moveToVisible=(name,columns,keys)=>{const st=getState(name,datasets[name].defaultSort);keys.forEach(key=>{if(!st.visibleOrder.includes(key))st.visibleOrder.push(key)});st.availableSelection=[];renderDataset(name);};
  const moveToAvailable=(name,keys)=>{const st=getState(name,datasets[name].defaultSort);st.visibleOrder=st.visibleOrder.filter(key=>!keys.includes(key));st.visibleSelection=[];renderDataset(name);};
  const renderPager=(name,rows,pageCount,start,end)=>{const st=getState(name,datasets[name].defaultSort),sizeOpts=[10,25,50,100].map(size=>`<option value="${size}"${size===st.size?' selected':''}>${size}</option>`).join('');const prevIcon='<span class="icon"><svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path d="m15 18-6-6 6-6"/></svg></span>';const nextIcon='<span class="icon"><svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path d="m9 18 6-6-6-6"/></svg></span>';const prevTip=esc(txt('pager_prev_tip','Pagina anterior'));const nextTip=esc(txt('pager_next_tip','Proxima pagina'));const pageStatus=esc(txt('pager_page_of','Pagina {0} de {1}').replace('{0}',String(st.page)).replace('{1}',String(pageCount)));const prevPage=Math.max(1,st.page-1),nextPage=Math.min(pageCount,st.page+1);return `<div class="pager"><div class="muted">${esc(txt('pager_showing_of','Showing {0}-{1} of {2}').replace('{0}',rows.length?String(start):'0').replace('{1}',String(end)).replace('{2}',String(rows.length)))}</div><div class="pager-main"><button type="button" class="icon-only" data-page-target="${prevPage}" onclick="return window.__akkornPage ? window.__akkornPage('${esc(name)}', ${prevPage}) : false;" aria-label="${prevTip}" title="${prevTip}"${st.page<=1?' disabled':''}>${prevIcon}</button><span class="pager-status">${pageStatus}</span><span class="pager-chip">${esc(String(st.page))}</span><input id="${name}PageInput" class="input page-input" type="number" min="1" max="${pageCount}" value="${st.page}" title="${esc(txt('pager_jump_tip','Ir para a pagina'))}"/><span class="pager-chip">${esc(String(pageCount))}</span><button type="button" class="icon-only" data-page-target="${nextPage}" onclick="return window.__akkornPage ? window.__akkornPage('${esc(name)}', ${nextPage}) : false;" aria-label="${nextTip}" title="${nextTip}"${st.page>=pageCount?' disabled':''}>${nextIcon}</button><select id="${name}PageSize" class="input page-size" title="${esc(txt('pager_size_tip','Tamanho da pagina'))}">${sizeOpts}</select></div></div>`;};
  const renderCell=(value,kind)=>{const text=display(value),typeClass=value===null||value===undefined||value===''?'type-null':`type-${kind||'text'}`;if(text.length<=140)return`<div class="cell-content"><span class="cell-text ${typeClass}" title="${esc(text)}">${esc(text)}</span></div>`;const eye='<span class="icon"><svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path d="M2.062 12.348a1 1 0 0 1 0-.696 10.75 10.75 0 0 1 19.876 0 1 1 0 0 1 0 .696 10.75 10.75 0 0 1-19.876 0"/><circle cx="12" cy="12" r="3"/></svg></span>';return`<div class="cell-content"><span class="cell-text truncated ${typeClass}" title="${esc(text)}">${esc(`${text.slice(0,140)}...`)}</span><button type="button" class="cell-expand with-icon" data-cell-modal="${esc(text)}">${eye}<span data-label>${esc(txt('cell_expand','View'))}</span></button></div>`;};
  const openCellModal=text=>{if($('cellModalText'))$('cellModalText').textContent=text||'';openModal('cellModal');};
  const bindCells=host=>host.querySelectorAll('[data-cell-modal]').forEach(btn=>btn.onclick=()=>openCellModal(btn.getAttribute('data-cell-modal')||''));
  window.__akkornPage=(name,target)=>{const dataset=datasets[name];if(!dataset)return false;const columns=dataset.columns(),st=getState(name,dataset.defaultSort),rows=applyFilters(name,dataset.rows,columns),pageCount=Math.max(1,Math.ceil(rows.length/toPositiveInt(st.size,10)));st.page=Math.min(Math.max(1,toPositiveInt(target,1)),pageCount);renderDataset(name);return false;};
  const selectTextInCell=cell=>{const content=cell.querySelector('.cell-text');if(!content)return;const range=document.createRange();range.selectNodeContents(content);const sel=window.getSelection();sel.removeAllRanges();sel.addRange(range);};
  const renderDataset=name=>{const dataset=datasets[name],host=$(`${name}Table`);if(!dataset||!host)return;const columns=dataset.columns(),st=getState(name,dataset.defaultSort);ensureVisible(name,columns);if(!columns.length){host.innerHTML=`<div class="empty">${esc(txt('table_no_rows','No rows available.'))}</div>`;renderFilterChips(name);renderPickList(name,columns);renderColumnsSummary(name,columns);renderSortSummary(name);save();return}if(!st.sortKey||!columns.some(col=>col.key===st.sortKey))st.sortKey=dataset.defaultSort||(columns[0]||{}).key||'';st.page=toPositiveInt(st.page,1);st.size=toPositiveInt(st.size,10);let rows=applyFilters(name,dataset.rows,columns).map((row,index)=>({row,rowId:index}));rows.sort((l,r)=>{const col=columns.find(item=>item.key===st.sortKey)||columns[0];if(!col)return 0;const result=compare(rowValue(l.row,col.key),rowValue(r.row,col.key),col.kind);return st.sortDir==='desc'?-result:result;});const visible=((st.visibleOrder.length?st.visibleOrder:columns.map(c=>c.key)).map(key=>columns.find(c=>c.key===key)).filter(Boolean));const safeVisible=visible.length?visible:columns;const pageCount=Math.max(1,Math.ceil(rows.length/st.size));st.page=Math.min(Math.max(1,toPositiveInt(st.page,1)),pageCount);const startIndex=(st.page-1)*st.size,pageRows=rows.slice(startIndex,startIndex+st.size);const body=pageRows.length?pageRows.map(item=>`<tr data-row-id="${item.rowId}" class="${st.selectedRows.includes(item.rowId)?'selected-row':''}"><td class="row-select-cell"><button type="button" class="row-select-btn${st.selectedRows.includes(item.rowId)?' active':''}" data-row-toggle="${item.rowId}"></button></td>${safeVisible.map(col=>`<td data-cell-key="${esc(col.key)}" data-row-id="${item.rowId}" class="${st.selectedCell&&st.selectedCell.rowId===item.rowId&&st.selectedCell.key===col.key?'selected-cell':''}">${renderCell(rowValue(item.row,col.key),col.kind)}</td>`).join('')}</tr>`).join(''):`<tr><td colspan="${Math.max(2,safeVisible.length+1)}">${esc(txt('table_no_rows','No rows available.'))}</td></tr>`;host.innerHTML=`<div class="tbl"><table><thead><tr><th class="row-select-head"></th>${safeVisible.map(col=>`<th data-key="${esc(col.key)}" class="${st.sortKey===col.key?(st.sortDir==='asc'?'th-sorted-asc':'th-sorted-desc'):''}">${esc(col.label)}</th>`).join('')}</tr></thead><tbody>${body}</tbody></table></div>${renderPager(name,rows,pageCount,rows.length?startIndex+1:0,Math.min(startIndex+st.size,rows.length))}`;host.querySelectorAll('th[data-key]').forEach(header=>header.onclick=()=>{const key=header.getAttribute('data-key');if(st.sortKey===key)st.sortDir=st.sortDir==='asc'?'desc':'asc';else{st.sortKey=key;st.sortDir='asc'}st.page=1;renderDataset(name);});host.querySelectorAll('[data-row-toggle]').forEach(button=>button.onclick=()=>{const rowId=parseInt(button.getAttribute('data-row-toggle')||'-1',10);st.selectedRows=st.selectedRows.includes(rowId)?st.selectedRows.filter(id=>id!==rowId):[...st.selectedRows,rowId];renderDataset(name);});host.querySelectorAll('td[data-cell-key]').forEach(cell=>cell.onclick=e=>{if(e.target.closest('[data-cell-modal]'))return;const rowId=parseInt(cell.getAttribute('data-row-id')||'-1',10),key=cell.getAttribute('data-cell-key');st.selectedCell={rowId,key};renderDataset(name);setTimeout(()=>selectTextInCell(host.querySelector(`td[data-row-id="${rowId}"][data-cell-key="${CSS.escape(key)}"]`)),0);});host.querySelectorAll('button[data-page-target]').forEach(button=>button.addEventListener('click',e=>{e.preventDefault();e.stopPropagation();const target=toPositiveInt(button.getAttribute('data-page-target'),st.page);if(target===st.page)return;st.page=Math.min(Math.max(1,target),pageCount);renderDataset(name);}));const pageInput=$(`${name}PageInput`);if(pageInput){pageInput.onchange=()=>{const value=toPositiveInt(pageInput.value,1);st.page=Math.min(Math.max(1,value),pageCount);renderDataset(name);};pageInput.onkeydown=e=>{if(e.key==='Enter'){e.preventDefault();pageInput.blur();}};}const pageSize=$(`${name}PageSize`);if(pageSize)pageSize.onchange=()=>{st.size=toPositiveInt(pageSize.value,10);st.page=1;renderDataset(name);};renderFilterChips(name);renderPickList(name,columns);renderColumnsSummary(name,columns);renderSortSummary(name);bindCells(host);save();};
  const bindDataset=name=>{const dataset=datasets[name];if(!dataset||!$(name))return;const columns=dataset.columns(),st=getState(name,dataset.defaultSort);ensureVisible(name,columns);const search=$(`${name}Search`);if(search){search.value=st.search||'';search.oninput=()=>{st.search=search.value;st.page=1;renderDataset(name);}}const filterCol=$(`${name}FilterCol`);if(filterCol){filterCol.innerHTML=columns.map(col=>`<option value="${esc(col.key)}">${esc(col.label)}</option>`).join('');filterCol.onchange=()=>renderValueInput(name);renderValueInput(name);}const sortCol=$(`${name}SortCol`),sortDir=$(`${name}SortDir`);if(sortCol){sortCol.innerHTML=columns.map(col=>`<option value="${esc(col.key)}"${col.key===st.sortKey?' selected':''}>${esc(col.label)}</option>`).join('');}if(sortDir){sortDir.value=st.sortDir||'asc';}if($(`${name}OpenFilters`))$(`${name}OpenFilters`).onclick=()=>openModal(`${name}FilterModal`);if($(`${name}OpenSort`))$(`${name}OpenSort`).onclick=()=>openModal(`${name}SortModal`);if($(`${name}OpenColumns`))$(`${name}OpenColumns`).onclick=()=>openModal(`${name}ColumnsModal`);if($(`${name}ColsDone`))$(`${name}ColsDone`).onclick=()=>closeModal(`${name}ColumnsModal`);if($(`${name}SortApply`))$(`${name}SortApply`).onclick=()=>{if(sortCol)st.sortKey=sortCol.value;if(sortDir)st.sortDir=sortDir.value;st.page=1;closeModal(`${name}SortModal`);renderDataset(name);};if($(`${name}SortClear`))$(`${name}SortClear`).onclick=()=>{st.sortKey=dataset.defaultSort||(columns[0]||{}).key||'';st.sortDir='asc';renderDataset(name);};if($(`${name}SortReset`))$(`${name}SortReset`).onclick=()=>{st.sortKey=dataset.defaultSort||(columns[0]||{}).key||'';st.sortDir='asc';renderDataset(name);};const inlineToggle=$(`${name}InlineFiltersToggle`),wrap=$(`${name}FiltersWrap`);if(wrap)wrap.classList.toggle('collapsed',!st.filtersExpanded);if(inlineToggle){setLabel(`${name}InlineFiltersToggle`,st.filtersExpanded?txt('button_collapse','Collapse'):txt('button_expand','Expand'));inlineToggle.onclick=()=>{st.filtersExpanded=!st.filtersExpanded;$(`${name}FiltersWrap`)?.classList.toggle('collapsed',!st.filtersExpanded);setLabel(`${name}InlineFiltersToggle`,st.filtersExpanded?txt('button_collapse','Collapse'):txt('button_expand','Expand'));save();};}const colsToggle=$(`${name}ColsInlineToggle`),colsWrap=$(`${name}ColsWrap`);if(colsWrap)colsWrap.classList.toggle('collapsed',!st.columnsExpanded);if(colsToggle){setLabel(`${name}ColsInlineToggle`,st.columnsExpanded?txt('button_collapse','Collapse'):txt('button_expand','Expand'));colsToggle.onclick=()=>{st.columnsExpanded=!st.columnsExpanded;$(`${name}ColsWrap`)?.classList.toggle('collapsed',!st.columnsExpanded);setLabel(`${name}ColsInlineToggle`,st.columnsExpanded?txt('button_collapse','Collapse'):txt('button_expand','Expand'));save();};}if($(`${name}Add`))$(`${name}Add`).onclick=()=>{const valueControl=$(`${name}Value`),opControl=$(`${name}Op`);if(!filterCol||!valueControl||!opControl||valueControl.value==='')return;st.filters.push({column:filterCol.value,operator:opControl.value,value:valueControl.value});st.filtersExpanded=true;st.page=1;closeModal(`${name}FilterModal`);renderDataset(name);};if($(`${name}Clear`))$(`${name}Clear`).onclick=()=>{st.filters=[];st.page=1;renderDataset(name);};if($(`${name}Reset`))$(`${name}Reset`).onclick=()=>{state[name]={page:1,size:10,search:'',sortKey:dataset.defaultSort||(columns[0]||{}).key||'',sortDir:'asc',filters:[],visibleOrder:columns.map(col=>col.key),availableSelection:[],visibleSelection:[],colSearch:'',filtersExpanded:false,columnsExpanded:false,selectedRows:[],selectedCell:null};bindDataset(name);renderDataset(name);};const colSearch=$(`${name}ColsSearch`);if(colSearch){colSearch.value=st.colSearch||'';colSearch.oninput=()=>{st.colSearch=colSearch.value;renderPickList(name,columns);save();};}if($(`${name}ColsAdd`))$(`${name}ColsAdd`).onclick=()=>moveToVisible(name,columns,st.availableSelection);if($(`${name}ColsRemove`))$(`${name}ColsRemove`).onclick=()=>moveToAvailable(name,st.visibleSelection);if($(`${name}ColsAll`))$(`${name}ColsAll`).onclick=()=>moveToVisible(name,columns,columns.filter(col=>!st.visibleOrder.includes(col.key)).map(col=>col.key));if($(`${name}ColsNone`))$(`${name}ColsNone`).onclick=()=>{st.visibleOrder=[];st.availableSelection=[];st.visibleSelection=[];renderDataset(name);};if($(`${name}ColsReset`))$(`${name}ColsReset`).onclick=()=>{st.visibleOrder=columns.map(col=>col.key);st.availableSelection=[];st.visibleSelection=[];renderDataset(name);};renderDataset(name);};
  const flash=(id,value)=>{const button=$(id);if(!button)return;const target=button.querySelector('[data-label]');const original=target?target.textContent:button.textContent;if(target)target.textContent=value;else button.textContent=value;setTimeout(()=>{if(target)target.textContent=original;else button.textContent=original;},1200);};
  const copyText=(value,done)=>{if(!value)return;if(navigator.clipboard&&navigator.clipboard.writeText)navigator.clipboard.writeText(value).finally(done);else done();};
  document.addEventListener('DOMContentLoaded',()=>{setTheme(theme);document.querySelectorAll('[data-close-modal]').forEach(button=>button.onclick=()=>closeModal(button.getAttribute('data-close-modal')));document.querySelectorAll('[data-collapse]').forEach(button=>{const sectionId=button.getAttribute('data-collapse'),section=$(sectionId);if(!section)return;if(collapsed[sectionId])section.classList.add('collapsed');button.textContent=section.classList.contains('collapsed')?txt('button_expand','Expand'):txt('button_collapse','Collapse');button.onclick=()=>{section.classList.toggle('collapsed');collapsed[sectionId]=section.classList.contains('collapsed');button.textContent=collapsed[sectionId]?txt('button_expand','Expand'):txt('button_collapse','Collapse');save();};});renderOverview();if($('sqlPre'))$('sqlPre').textContent=SQL||txt('sql_not_available','-- SQL not available.');if($('footer'))$('footer').innerHTML=`${esc(txt('footer_generated_by','Generated by'))} <strong>AkkornStudio</strong> · ${esc(META.generatedAt||'')} · v__REPORT_VERSION__`;['results','schema','metadata','lineageNodes','lineageConnections'].forEach(bindDataset);if($('themeBtn'))$('themeBtn').onclick=()=>setTheme(theme==='dark'?'light':'dark');if($('summaryBtn'))$('summaryBtn').onclick=()=>copyText([META.title,`status: ${META.status}`,`rows: ${META.rowCount}`,`columns: ${META.columnCount}`,`executionTimeMs: ${META.executionTimeMs}`,`joins: ${META.joinCount}`,`aggregates: ${META.aggregateCount}`,`hasSubquery: ${META.hasSubquery}`].join('\n'),()=>flash('summaryBtn',txt('button_copied','Copied')));if($('sqlBtn'))$('sqlBtn').onclick=()=>copyText(SQL,()=>flash('sqlBtn',txt('button_copied','Copied')));if($('csvBtn'))$('csvBtn').onclick=()=>{const columns=datasets.results.columns(),st=getState('results',datasets.results.defaultSort),visible=(st.visibleOrder.length?st.visibleOrder:columns.map(col=>col.key)).map(key=>columns.find(col=>col.key===key)).filter(Boolean),safeVisible=visible.length?visible:columns,rows=applyFilters('results',ROWS,columns).sort((l,r)=>{const col=columns.find(item=>item.key===st.sortKey)||columns[0];const result=col?compare(rowValue(l,col.key),rowValue(r,col.key),col.kind):0;return st.sortDir==='desc'?-result:result;});const csv=[safeVisible.map(col=>`"${col.label.replace(/"/g,'""')}"`).join(',')].concat(rows.map(row=>safeVisible.map(col=>`"${display(rowValue(row,col.key)).replace(/"/g,'""')}"`).join(','))).join('\n');const blob=new Blob([csv],{type:'text/csv;charset=utf-8'}),link=document.createElement('a');link.href=URL.createObjectURL(blob);link.download=`${META.title||'report'}-visible.csv`;document.body.appendChild(link);link.click();document.body.removeChild(link);URL.revokeObjectURL(link.href);flash('csvBtn',txt('button_exported','Exported'));};if($('printBtn'))$('printBtn').onclick=()=>window.print();});
})();
""";

    private static object BuildLabels(string language) => language.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
        ? new
        {
            theme_dark = "Escuro",
            theme_light = "Claro",
            button_expand = "Expandir",
            button_collapse = "Recolher",
            button_copied = "Copiado",
            button_exported = "Exportado",
            filter_value_placeholder = "Valor do filtro",
            filters_none_active = "Nenhum filtro ativo.",
            summary_status = "Status",
            summary_execution_time = "Tempo de execucao",
            summary_rows = "Linhas",
            summary_columns = "Colunas",
            summary_joins = "Joins",
            summary_aggregates = "Agregacoes",
            summary_subquery = "Subquery",
            summary_query_length = "Tamanho da query",
            sort_asc = "Crescente",
            sort_desc = "Decrescente",
            sort_none = "Nenhuma ordenacao ativa.",
            overview_generated_at = "Gerado",
            overview_description = "Descricao",
            overview_file_path = "Arquivo",
            overview_ok = "Execucao concluida sem mensagens adicionais.",
            footer_generated_by = "Gerado por",
            table_no_rows = "Nenhuma linha disponivel.",
            pager_showing_of = "Exibindo {0}-{1} de {2}",
            pager_page_of = "Pagina {0} de {1}",
            pager_prev_icon = "<",
            pager_next_icon = ">",
            pager_prev_tip = "Pagina anterior",
            pager_next_tip = "Proxima pagina",
            pager_jump_tip = "Ir para a pagina",
            pager_size_tip = "Tamanho da pagina",
            field_column = "Coluna",
            field_type = "Tipo",
            field_nulls = "Nulos",
            field_distinct = "Distintos",
            field_example = "Exemplo",
            field_min = "Minimo",
            field_max = "Maximo",
            field_field = "Campo",
            field_label = "Rotulo",
            field_value = "Valor",
            field_category = "Categoria",
            field_title = "Titulo",
            field_status = "Status",
            field_from_node = "No de origem",
            field_from_pin = "Pino de origem",
            field_to_node = "No de destino",
            field_to_pin = "Pino de destino",
            field_data_type = "Tipo de dado",
            columns_none_available = "Nenhuma coluna disponivel.",
            columns_none_showing = "Nenhuma coluna visivel.",
            cell_expand = "Ver",
            sql_not_available = "-- SQL indisponivel."
        }
        : new
        {
            theme_dark = "Dark",
            theme_light = "Light",
            button_expand = "Expand",
            button_collapse = "Collapse",
            button_copied = "Copied",
            button_exported = "Exported",
            filter_value_placeholder = "Filter value",
            filters_none_active = "No filters active.",
            summary_status = "Status",
            summary_execution_time = "Execution time",
            summary_rows = "Rows",
            summary_columns = "Columns",
            summary_joins = "Joins",
            summary_aggregates = "Aggregates",
            summary_subquery = "Subquery",
            summary_query_length = "Query length",
            sort_asc = "Ascending",
            sort_desc = "Descending",
            sort_none = "No active sorting.",
            overview_generated_at = "Generated",
            overview_description = "Description",
            overview_file_path = "File path",
            overview_ok = "Execution completed without additional messages.",
            footer_generated_by = "Generated by",
            table_no_rows = "No rows available.",
            pager_showing_of = "Showing {0}-{1} of {2}",
            pager_page_of = "Page {0} of {1}",
            pager_prev_icon = "<",
            pager_next_icon = ">",
            pager_prev_tip = "Previous page",
            pager_next_tip = "Next page",
            pager_jump_tip = "Jump to page",
            pager_size_tip = "Page size",
            field_column = "Column",
            field_type = "Type",
            field_nulls = "Nulls",
            field_distinct = "Distinct",
            field_example = "Example",
            field_min = "Min",
            field_max = "Max",
            field_field = "Field",
            field_label = "Label",
            field_value = "Value",
            field_category = "Category",
            field_title = "Title",
            field_status = "Status",
            field_from_node = "From node",
            field_from_pin = "From pin",
            field_to_node = "To node",
            field_to_pin = "To pin",
            field_data_type = "Data type",
            columns_none_available = "No columns available.",
            columns_none_showing = "No visible columns.",
            cell_expand = "View",
            sql_not_available = "-- SQL not available."
        };

    private static string ResolveLanguage() => CultureInfo.CurrentUICulture.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? "pt-BR" : "en";
    private static string Txt(string language, string pt, string en) => language.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? pt : en;
    private static string IconSvg(string name) => $"<span class=\"icon\">{name switch
    {
        "moon" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path d=\"M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9\"/></svg>",
        "sun" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><circle cx=\"12\" cy=\"12\" r=\"4\"/><path d=\"M12 2v2\"/><path d=\"M12 20v2\"/><path d=\"m4.93 4.93 1.41 1.41\"/><path d=\"m17.66 17.66 1.41 1.41\"/><path d=\"M2 12h2\"/><path d=\"M20 12h2\"/><path d=\"m6.34 17.66-1.41 1.41\"/><path d=\"m19.07 4.93-1.41 1.41\"/></svg>",
        "clipboard" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><rect width=\"8\" height=\"4\" x=\"8\" y=\"2\" rx=\"1\" ry=\"1\"/><path d=\"M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2\"/></svg>",
        "code" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><polyline points=\"16 18 22 12 16 6\"/><polyline points=\"8 6 2 12 8 18\"/></svg>",
        "download" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path d=\"M12 3v12\"/><path d=\"m7 10 5 5 5-5\"/><path d=\"M5 21h14\"/></svg>",
        "printer" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path d=\"M6 9V2h12v7\"/><path d=\"M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2\"/><path d=\"M6 14h12v8H6z\"/></svg>",
        "filter" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><polygon points=\"22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3\"/></svg>",
        "funnel" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path d=\"M10 18h4\"/><path d=\"M3 6h18\"/><path d=\"M6 12h12\"/></svg>",
        "columns" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><rect x=\"3\" y=\"4\" width=\"6\" height=\"16\" rx=\"1\"/><rect x=\"9\" y=\"4\" width=\"6\" height=\"16\" rx=\"1\"/><rect x=\"15\" y=\"4\" width=\"6\" height=\"16\" rx=\"1\"/></svg>",
        "rotate" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path d=\"M21 2v6h-6\"/><path d=\"M3 12a9 9 0 0 1 15.55-6.36L21 8\"/><path d=\"M3 22v-6h6\"/><path d=\"M21 12a9 9 0 0 1-15.55 6.36L3 16\"/></svg>",
        "sort" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path d=\"M11 5h10\"/><path d=\"M11 9h7\"/><path d=\"M11 13h4\"/><path d=\"m3 17 3 3 3-3\"/><path d=\"M6 4v16\"/></svg>",
        "arrow-up-down" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path d=\"m7 15 5 5 5-5\"/><path d=\"m7 9 5-5 5 5\"/><path d=\"M12 4v16\"/></svg>",
        "eraser" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path d=\"m7 21 9.59-9.59a2 2 0 0 0 0-2.82l-3.18-3.18a2 2 0 0 0-2.82 0L1 15\"/><path d=\"m8 17 5 5\"/><path d=\"M22 21H7\"/></svg>",
        "chevron-down" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path d=\"m6 9 6 6 6-6\"/></svg>",
        "x" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path d=\"M18 6 6 18\"/><path d=\"m6 6 12 12\"/></svg>",
        "plus" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path d=\"M12 5v14\"/><path d=\"M5 12h14\"/></svg>",
        "check" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path d=\"M20 6 9 17l-5-5\"/></svg>",
        "text" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path d=\"M4 7V4h16v3\"/><path d=\"M9 20h6\"/><path d=\"M12 4v16\"/></svg>",
        _ => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><circle cx=\"12\" cy=\"12\" r=\"8\"/></svg>",
    }}</span>";
    private static string EmptyValueMode(SqlEditorReportEmptyValueDisplayMode mode) => mode switch { SqlEditorReportEmptyValueDisplayMode.Dash => "dash", SqlEditorReportEmptyValueDisplayMode.NullLiteral => "null", _ => "blank" };
    private static string InferDialect(DatabaseProvider? provider) => provider switch { DatabaseProvider.Postgres => "postgresql", DatabaseProvider.SqlServer => "sqlserver", DatabaseProvider.MySql => "mysql", DatabaseProvider.SQLite => "sqlite", _ => "unknown" };
    private static string NormalizeStatus(string? status) => string.IsNullOrWhiteSpace(status) ? "success" : status.Trim().ToLowerInvariant() switch { "error" => "error", "warning" => "warning", _ => "success" };
    private static int CountMatches(string input, string pattern) => string.IsNullOrWhiteSpace(input) ? 0 : Regex.Matches(input, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count;
    private static bool ContainsSubquery(string sql) => !string.IsNullOrWhiteSpace(sql) && (Regex.IsMatch(sql, @"\(\s*select\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) || Regex.IsMatch(sql, @"\bexists\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    private static string DetectKind(object value) => value switch { bool => "bool", byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => "number", DateTime or DateTimeOffset => "date", _ => "text" };
    private static string MergeKinds(string left, string right) => left == right ? left : "text";
    private static string Html(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

    private static string Json(object value)
    {
        string json = JsonSerializer.Serialize(value, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        return Regex.Replace(json, "</script", "<\\/script", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}

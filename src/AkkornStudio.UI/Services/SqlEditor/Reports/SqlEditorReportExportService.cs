using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using AkkornStudio.Core;
using AkkornStudio.UI.Services.Theming;
using AkkornStudio.UI.ViewModels;

namespace AkkornStudio.UI.Services.SqlEditor.Reports;

public sealed class SqlEditorReportExportService
{
    private const string ReportVersion = "2.3";

    public async Task<string> ExportAsync(
        SqlEditorReportExportContext context,
        SqlEditorReportExportRequest request,
        CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(request.FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        switch (request.ReportType)
        {
          case SqlEditorReportType.HtmlFullFeature:
            await File.WriteAllTextAsync(request.FilePath, BuildHtml(context, request), Encoding.UTF8, cancellationToken);
            break;
          case SqlEditorReportType.JsonContract:
            await File.WriteAllTextAsync(request.FilePath, BuildJson(context, request), Encoding.UTF8, cancellationToken);
            break;
          case SqlEditorReportType.CsvData:
            await File.WriteAllTextAsync(request.FilePath, BuildCsv(context), new UTF8Encoding(true), cancellationToken);
            break;
          case SqlEditorReportType.ExcelWorkbook:
            await File.WriteAllBytesAsync(request.FilePath, BuildExcelWorkbook(context), cancellationToken);
            break;
          default:
            throw new NotSupportedException($"Unsupported report type: {request.ReportType}");
        }

        return Path.GetFullPath(request.FilePath);
    }

    private static string BuildJson(SqlEditorReportExportContext context, SqlEditorReportExportRequest request)
    {
        object payload = BuildJsonPayload(context, request);
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    private static string BuildCsv(SqlEditorReportExportContext context)
    {
      IReadOnlyList<string> columns = GetDataColumns(context);
      var rows = new List<string>();

      if (columns.Count > 0)
        rows.Add(string.Join(',', columns.Select(EscapeCsvCell)));

      foreach (IReadOnlyDictionary<string, object?> row in context.ResultRows)
      {
        rows.Add(string.Join(',', columns.Select(column => EscapeCsvCell(row.TryGetValue(column, out object? value) ? value : null))));
      }

      return string.Join(Environment.NewLine, rows);
    }

    private static byte[] BuildExcelWorkbook(SqlEditorReportExportContext context)
    {
      IReadOnlyList<string> columns = GetDataColumns(context);

      using var workbook = new XLWorkbook();
      IXLWorksheet worksheet = workbook.Worksheets.Add("Results");

      int currentRow = 1;
      if (columns.Count > 0)
      {
        for (int columnIndex = 0; columnIndex < columns.Count; columnIndex += 1)
          worksheet.Cell(currentRow, columnIndex + 1).Value = columns[columnIndex];

        worksheet.Row(currentRow).Style.Font.Bold = true;
        currentRow += 1;
      }

      foreach (IReadOnlyDictionary<string, object?> row in context.ResultRows)
      {
        for (int columnIndex = 0; columnIndex < columns.Count; columnIndex += 1)
        {
          string column = columns[columnIndex];
          row.TryGetValue(column, out object? value);
          worksheet.Cell(currentRow, columnIndex + 1).SetValue(value?.ToString() ?? string.Empty);
        }

        currentRow += 1;
      }

      worksheet.Columns().AdjustToContents();

      using var stream = new MemoryStream();
      workbook.SaveAs(stream);
      return stream.ToArray();
    }

    private static IReadOnlyList<string> GetDataColumns(SqlEditorReportExportContext context)
    {
      if (context.SchemaColumns.Count > 0)
        return context.SchemaColumns;

      if (context.ResultRows.Count > 0)
        return [.. context.ResultRows[0].Keys];

      return [];
    }

    private static string EscapeCsvCell(object? value)
    {
      string text = value?.ToString() ?? string.Empty;
      if (text.IndexOfAny([',', '"', '\n', '\r']) < 0)
        return text;

      return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private static string BuildHtml(SqlEditorReportExportContext context, SqlEditorReportExportRequest request)
    {
        string generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        string generatedAtIso = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
        object meta = BuildMeta(context, request, generatedAt, generatedAtIso);
        object result = BuildExecutionResultObject(context.ExecutionResult);
        bool includeGraphSections = request.IncludeMetadata && request.IncludeNodeDetails;
        object[]? nodes = includeGraphSections ? [] : null;
        object[]? connections = includeGraphSections ? [] : null;
        bool hasSql = !string.IsNullOrWhiteSpace(context.Sql);

        string metaJson = SerializeForInlineJs(meta);
        string resultJson = SerializeForInlineJs(result);
        string schemaJson = SerializeForInlineJs(request.IncludeSchema ? context.SchemaColumns : []);
        string resultRowsJson = SerializeForInlineJs(context.ResultRows);
        string nodesJson = nodes is null ? "null" : SerializeForInlineJs(nodes);
        string connectionsJson = connections is null ? "null" : SerializeForInlineJs(connections);
        string hasSqlJson = hasSql ? "true" : "false";
        string sqlEscaped = EscapeJsTemplateLiteral(context.Sql);
        string reportLanguage = ResolveReportLanguageTag();
        object labels = BuildReportLabels(reportLanguage);
        string labelsJson = SerializeForInlineJs(labels);

        string title = string.IsNullOrWhiteSpace(request.Title) ? context.TabTitle : request.Title;
        string description = string.IsNullOrWhiteSpace(request.Description)
          ? (request.UseDashForEmptyFields ? "-" : "null")
          : request.Description.Trim();

        return $$"""
<!DOCTYPE html>
      <html lang="{{HtmlEncode(reportLanguage)}}" data-theme="dark">
<head>
<meta charset="UTF-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1.0"/>
      <title>{{HtmlEncode(title)}} - {{HtmlEncode(reportLanguage.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? "Relatorio AkkornStudio" : "AkkornStudio Report")}}</title>
<style>
* { box-sizing: border-box; margin: 0; padding: 0; }
:root[data-theme="dark"] {
  --bg: {{UiColorConstants.C_090B14.ToLowerInvariant()}};
  --bg-panel: {{UiColorConstants.C_12172A.ToLowerInvariant()}};
  --bg-panel-strong: {{UiColorConstants.C_12172A.ToLowerInvariant()}};
  --bg-surface: {{UiColorConstants.C_1E2A3F.ToLowerInvariant()}};
  --bg-surface-2: {{UiColorConstants.C_252C3F.ToLowerInvariant()}};
  --border: {{UiColorConstants.C_2A3554.ToLowerInvariant()}};
  --border-strong: {{UiColorConstants.C_334164.ToLowerInvariant()}};
  --text: {{UiColorConstants.C_E7ECFF.ToLowerInvariant()}};
  --text-muted: {{UiColorConstants.C_AEB9D9.ToLowerInvariant()}};
  --accent: {{UiColorConstants.C_5B7CFA.ToLowerInvariant()}};
  --accent-2: {{UiColorConstants.C_818CF8.ToLowerInvariant()}};
  --code-bg: {{UiColorConstants.C_0F1220.ToLowerInvariant()}};
  --shadow: none;
}
:root[data-theme="light"] {
  --bg: {{UiColorConstants.C_F0F2FA.ToLowerInvariant()}};
  --bg-panel: {{UiColorConstants.C_FFFFFF.ToLowerInvariant()}};
  --bg-panel-strong: {{UiColorConstants.C_FFFFFF.ToLowerInvariant()}};
  --bg-surface: {{UiColorConstants.C_F5F6FB.ToLowerInvariant()}};
  --bg-surface-2: {{UiColorConstants.C_EAECF5.ToLowerInvariant()}};
  --border: {{UiColorConstants.C_D0D4E8.ToLowerInvariant()}};
  --border-strong: {{UiColorConstants.C_B8BDD6.ToLowerInvariant()}};
  --text: {{UiColorConstants.C_1A1D2E.ToLowerInvariant()}};
  --text-muted: {{UiColorConstants.C_4A4F6A.ToLowerInvariant()}};
  --accent: {{UiColorConstants.C_5B7CFA.ToLowerInvariant()}};
  --accent-2: {{UiColorConstants.C_818CF8.ToLowerInvariant()}};
  --code-bg: {{UiColorConstants.C_F7F8FD.ToLowerInvariant()}};
  --shadow: none;
}
body {
  font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
  background: var(--bg);
  color: var(--text);
  min-height: 100vh;
  line-height: 1.5;
  letter-spacing: 0.01em;
}
header {
  position: sticky;
  top: 0;
  z-index: 2;
  min-height: 56px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 14px;
  padding: 10px 18px;
  border-bottom: 1px solid var(--border);
  background: var(--bg-panel);
}
.logo {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-weight: 800;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--accent-2);
}
.logo::before {
  content: '';
  width: 10px;
  height: 10px;
  border-radius: 999px;
  background: var(--accent);
}
.header-center { flex: 1; min-width: 0; }
.header-center h1 {
  font-size: 16px;
  font-weight: 800;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.description {
  color: var(--text-muted);
  font-size: 12px;
  margin-top: 3px;
}
.shortcut-hint {
  max-width: 1320px;
  margin: 10px auto 0;
  padding: 0 16px;
}
.shortcut-hint-inner {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  border-radius: 12px;
  border: 1px solid var(--border);
  background: var(--bg-surface);
  color: var(--text-muted);
  font-size: 12px;
}
.shortcut-title {
  color: var(--text);
  font-weight: 700;
  margin-right: 2px;
}
.shortcut-item {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.shortcut-hint kbd {
  font-family: 'JetBrains Mono', 'Cascadia Mono', 'SFMono-Regular', Consolas, monospace;
  font-size: 11px;
  border: 1px solid var(--border-strong);
  border-bottom-width: 2px;
  border-radius: 8px;
  padding: 2px 7px;
  color: var(--text);
  background: var(--bg-surface);
}
main {
  max-width: 1320px;
  margin: 0 auto;
  padding: 20px 16px 28px;
  display: grid;
  gap: 18px;
}
section {
  background: var(--bg-panel);
  border: 1px solid var(--border-strong);
  border-radius: 16px;
  overflow: hidden;
}
.section-header {
  padding: 14px 16px;
  border-bottom: 1px solid var(--border);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  background: var(--bg-panel-strong);
}
.section-header h2 {
  font-size: 13px;
  text-transform: uppercase;
  letter-spacing: 0.12em;
  color: var(--text-muted);
  display: inline-flex;
  align-items: center;
  gap: 7px;
}
.section-icon {
  width: 16px;
  height: 16px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: var(--accent);
}
.section-icon svg {
  width: 14px;
  height: 14px;
  fill: currentColor;
}
.section-header .controls {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  align-items: center;
  gap: 8px;
}
.section-body {
  padding: 16px;
  max-height: 2200px;
  opacity: 1;
  overflow: hidden;
  transition: max-height 0.24s ease, opacity 0.18s ease, padding 0.18s ease;
}
.section.is-collapsed .section-body {
  max-height: 0;
  opacity: 0;
  padding-top: 0;
  padding-bottom: 0;
  pointer-events: none;
}
.meta-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 10px;
}
.meta-card {
  background: var(--bg-surface);
  border: 1px solid var(--border);
  border-radius: 12px;
  padding: 11px 12px;
}
.meta-card .label {
  font-size: 10px;
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: 0.12em;
}
.meta-card.full-width { grid-column: 1 / -1; }
.meta-card .value {
  font-size: 14px;
  margin-top: 5px;
  word-break: break-word;
}
.kpi-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 10px;
}
.kpi-card {
  background: var(--bg-surface);
  border: 1px solid var(--border);
  border-radius: 14px;
  padding: 14px;
}
.kpi-card .label {
  color: var(--text-muted);
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.12em;
}
.kpi-card .value {
  font-size: 18px;
  font-weight: 800;
  margin-top: 6px;
}
.kpi-card .hint {
  color: var(--text-muted);
  font-size: 12px;
  margin-top: 2px;
}
.banner {
  border-radius: 14px;
  padding: 12px 14px;
  margin-top: 12px;
  border: 1px solid var(--border);
}
.banner-ok { background: var(--bg-surface-2); border-color: {{UiColorConstants.C_2FBF84.ToLowerInvariant()}}; }
.banner-warn { background: var(--bg-surface-2); border-color: {{UiColorConstants.C_D9A441.ToLowerInvariant()}}; }
.banner-err { background: var(--bg-surface-2); border-color: {{UiColorConstants.C_E16174.ToLowerInvariant()}}; }
.sql-block {
  background: var(--code-bg);
  border: 1px solid var(--border-strong);
  border-radius: 14px;
  padding: 16px;
  overflow-x: auto;
}
pre {
  margin: 0;
  white-space: pre-wrap;
  word-break: break-word;
  font-family: 'JetBrains Mono', 'Cascadia Mono', 'SFMono-Regular', Consolas, monospace;
  font-size: 12px;
  line-height: 1.7;
}
table { width: 100%; border-collapse: collapse; }
th, td {
  padding: 9px 10px;
  border-bottom: 1px solid var(--border);
  text-align: left;
  vertical-align: top;
}
th {
  color: var(--text-muted);
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.12em;
  cursor: pointer;
  user-select: none;
}
th.sorted-asc::after { content: ' ↑'; color: var(--accent); }
th.sorted-desc::after { content: ' ↓'; color: var(--accent); }
.badge {
  border-radius: 999px;
  padding: 3px 10px;
  font-size: 10px;
  border: 1px solid var(--border);
  color: var(--text-muted);
  background: var(--bg-surface-2);
  text-transform: uppercase;
  letter-spacing: 0.08em;
}
.tbl-wrap {
  overflow-x: auto;
  border: 1px solid var(--border-strong);
  border-radius: 14px;
  background: var(--bg-surface);
}
.tbl-wrap table thead th {
  position: sticky;
  top: 0;
  background: var(--bg-surface-2);
}
.tbl-wrap table {
  width: max-content;
  min-width: 100%;
}
.tbl-wrap td,
.tbl-wrap th {
  white-space: nowrap;
}
.tbl-wrap tbody tr:nth-child(even) td {
  background: var(--bg-panel);
}
.status-success { color: {{UiColorConstants.C_2FBF84.ToLowerInvariant()}}; }
.status-warning { color: {{UiColorConstants.C_D9A441.ToLowerInvariant()}}; }
.status-error { color: {{UiColorConstants.C_E16174.ToLowerInvariant()}}; }
.type-null { color: var(--text-muted); font-style: italic; }
.type-text { color: var(--text); }
.type-number { color: var(--accent-2); }
.type-date { color: {{UiColorConstants.C_60A5FA.ToLowerInvariant()}}; }
.type-bool { color: {{UiColorConstants.C_D9A441.ToLowerInvariant()}}; }
.type-success { color: {{UiColorConstants.C_2FBF84.ToLowerInvariant()}}; }
.type-warning { color: {{UiColorConstants.C_D9A441.ToLowerInvariant()}}; }
.type-error { color: {{UiColorConstants.C_E16174.ToLowerInvariant()}}; }
.collapse-btn {
  font-size: 11px;
  border-radius: 999px;
  padding: 6px 12px;
  min-width: 90px;
  transition: transform 0.14s ease, border-color 0.14s ease;
}
.collapse-btn:hover {
  transform: translateY(-1px);
}
.collapse-btn.is-collapsed {
  border-color: var(--border-strong);
}
.filter-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 10px;
}
.tool-groups {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 8px;
}
.tool-toggle {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 10px;
  border-radius: 999px;
}
.btn-icon {
  width: 14px;
  height: 14px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}
.btn-icon svg {
  width: 12px;
  height: 12px;
  fill: currentColor;
}
.subpanel {
  border: 1px solid var(--border-strong);
  border-radius: 12px;
  padding: 10px;
  margin-bottom: 10px;
  background: var(--bg-panel-strong);
}
.subpanel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}
.subpanel-title {
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.11em;
  color: var(--text-muted);
}
.subpanel-body {
  margin-top: 8px;
}
.subpanel.is-collapsed .subpanel-body {
  display: none;
}
.filter-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 12px;
}
.acc-empty {
  color: var(--text-muted);
  font-size: 12px;
}
.filter-chip {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 6px 10px;
  border-radius: 999px;
  background: var(--bg-surface-2);
  border: 1px solid var(--accent);
}
.chip-col {
  color: var(--accent);
  font-weight: 700;
  font-family: 'Cascadia Mono', 'SFMono-Regular', Consolas, monospace;
}
.chip-op,
.chip-val {
  color: var(--text-muted);
  font-size: 12px;
}
.chip-rm {
  background: transparent;
  border: none;
  color: var(--text-muted);
  cursor: pointer;
  padding: 0;
  font-size: 14px;
  line-height: 1;
}
.chip-rm:hover {
  color: var(--text);
}
.table-input,
.table-select {
  background: var(--bg-surface);
  border: 1px solid var(--border);
  color: var(--text);
  border-radius: 10px;
  padding: 7px 10px;
  min-width: 160px;
}
.pager {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  border-top: 1px solid var(--border);
  margin-top: 10px;
  padding-top: 10px;
}
.pager-info {
  color: var(--text-muted);
  font-size: 12px;
}
.pager-btns {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
}
footer {
  text-align: center;
  color: var(--text-muted);
  font-size: 12px;
  padding: 18px 12px 24px;
}
button {
  background: var(--bg-surface);
  border: 1px solid var(--border);
  color: var(--text);
  border-radius: 10px;
  padding: 6px 12px;
  cursor: pointer;
  transition: transform 0.12s ease, border-color 0.12s ease, background 0.12s ease;
}
button:hover {
  border-color: var(--border-strong);
  transform: translateY(-1px);
}
button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
}
@media (max-width: 900px) {
  .section-header {
    align-items: flex-start;
    flex-direction: column;
  }

  .section-header .controls {
    width: 100%;
    justify-content: flex-start;
  }

  .table-input,
  .table-select {
    min-width: 140px;
    flex: 1 1 140px;
  }
}
</style>
</head>
<body>
<header>
  <span class="logo">AkkornStudio</span>
  <div class="header-center">
    <h1>{{HtmlEncode(title)}}</h1>
    <p class="description">{{HtmlEncode(description)}}</p>
  </div>
  <button id="btn-theme" type="button" data-i18n="theme_toggle">Toggle theme</button>
</header>
<div class="shortcut-hint" aria-label="Keyboard shortcuts" data-i18n-aria-label="keyboard_shortcuts_aria">
  <div class="shortcut-hint-inner">
    <span class="shortcut-title" data-i18n="shortcuts_title">Shortcuts</span>
    <span class="shortcut-item"><kbd>/</kbd> <span data-i18n="shortcuts_focus_search">Focus active search</span></span>
    <span class="shortcut-item"><kbd>Alt</kbd>+<kbd>R</kbd> <span data-i18n="shortcuts_results_filter">Results filter</span></span>
    <span class="shortcut-item"><kbd>Alt</kbd>+<kbd>M</kbd> <span data-i18n="shortcuts_metadata_filter">Metadata filter</span></span>
    <span class="shortcut-item"><kbd>Alt</kbd>+<kbd>N</kbd> <span data-i18n="shortcuts_nodes_filter">Nodes filter</span></span>
    <span class="shortcut-item"><kbd>Alt</kbd>+<kbd>C</kbd> <span data-i18n="shortcuts_connections_filter">Connections filter</span></span>
  </div>
</div>
<main>
  <section id="s-summary" class="is-collapsed">
    <div class="section-header">
      <h2 data-i18n="section_execution_summary">Execution Summary</h2>
      <div class="controls">
        <button class="collapse-btn" data-collapse-target="s-summary" type="button">▸ Expand</button>
      </div>
    </div>
    <div class="section-body" id="summary-body"></div>
  </section>

  <section id="s-quality" class="is-collapsed">
    <div class="section-header">
      <h2 data-i18n="section_validation_summary">Validation Summary</h2>
      <div class="controls">
        <button class="collapse-btn" data-collapse-target="s-quality" type="button">▸ Expand</button>
      </div>
    </div>
    <div class="section-body" id="quality-body"></div>
  </section>

  {{SqlEditorReportHtmlSections.BuildResultsSection()}}
  <section id="s-nodes" class="is-collapsed">
    <div class="section-header">
      <h2><span class="section-icon" aria-hidden="true"><svg viewBox="0 0 16 16" focusable="false"><path d="M3 2h4v4H3V2Zm6 0h4v4H9V2ZM3 8h4v4H3V8Zm6 0h4v4H9V8Z"/></svg></span><span data-i18n="section_node_inventory">Node Inventory</span></h2>
      <div class="controls">
        <input id="nodes-search" class="table-input" type="search" placeholder="Filter nodes" data-i18n-placeholder="filter_nodes_placeholder"/>
        <select id="nodes-page-size" class="table-select">
          <option value="10">10</option>
          <option value="25">25</option>
          <option value="50">50</option>
        </select>
        <button class="collapse-btn" data-collapse-target="s-nodes" type="button">▸ Expand</button>
      </div>
    </div>
    <div class="section-body">
      {{SqlEditorReportHtmlSections.BuildFilterAndOrderPanels("nodes")}}
      <div id="nodes-body"></div>
    </div>
  </section>

  <section id="s-conns" class="is-collapsed">
    <div class="section-header">
      <h2><span class="section-icon" aria-hidden="true"><svg viewBox="0 0 16 16" focusable="false"><path d="M3 3h4v3H3V3Zm6 0h4v3H9V3ZM6.5 7h3v2h-3V7Zm-3 3h4v3H3v-3Zm6 0h4v3H9v-3Z"/></svg></span><span data-i18n="section_connection_map">Connection Map</span></h2>
      <div class="controls">
        <input id="conns-search" class="table-input" type="search" placeholder="Filter connections" data-i18n-placeholder="filter_connections_placeholder"/>
        <select id="conns-page-size" class="table-select">
          <option value="10">10</option>
          <option value="25">25</option>
          <option value="50">50</option>
        </select>
        <button class="collapse-btn" data-collapse-target="s-conns" type="button">▸ Expand</button>
      </div>
    </div>
    <div class="section-body">
      {{SqlEditorReportHtmlSections.BuildFilterAndOrderPanels("conns")}}
      <div id="conns-body"></div>
    </div>
  </section>

  <section id="s-sql" class="is-collapsed">
    <div class="section-header">
      <h2 data-i18n="section_sql_query">SQL Query</h2>
      <div class="controls">
        <span class="badge" data-i18n="badge_primary_output">Primary Output</span>
        <button id="btn-copy-sql" class="collapse-btn" type="button" data-i18n="button_copy_sql">Copy SQL</button>
        <button class="collapse-btn" data-collapse-target="s-sql" type="button">▸ Expand</button>
      </div>
    </div>
    <div class="section-body">
      <div class="sql-block"><pre id="sql-pre"></pre></div>
    </div>
  </section>

  <section id="s-meta" class="is-collapsed">
    <div class="section-header">
      <h2><span class="section-icon" aria-hidden="true"><svg viewBox="0 0 16 16" focusable="false"><path d="M8 1a7 7 0 1 0 0 14A7 7 0 0 0 8 1Zm0 3.2a1 1 0 1 1 0 2 1 1 0 0 1 0-2Zm-1.1 3h2.1v4.5H6.9V7.2Z"/></svg></span><span data-i18n="section_optional_metadata">Optional Metadata</span></h2>
      <div class="controls">
        <input id="metadata-search" class="table-input" type="search" placeholder="Filter metadata" data-i18n-placeholder="filter_metadata_placeholder"/>
        <select id="metadata-page-size" class="table-select">
          <option value="10">10</option>
          <option value="25">25</option>
          <option value="50">50</option>
        </select>
        <button class="collapse-btn" data-collapse-target="s-meta" type="button">▸ Expand</button>
      </div>
    </div>
    <div class="section-body">
      {{SqlEditorReportHtmlSections.BuildFilterAndOrderPanels("metadata")}}
      <div id="metadata-table"></div>
    </div>
  </section>
</main>
<footer id="footer-text"></footer>

<script>
const REPORT_VERSION = "2.3";
const REPORT_META = {{metaJson}};
const EXECUTION_RESULT = {{resultJson}};
const SCHEMA_COLS = {{schemaJson}};
const RESULT_ROWS = {{resultRowsJson}};
const HAS_SQL = {{hasSqlJson}};
const SQL_TEXT = `{{sqlEscaped}}`;
const NODE_ROWS = {{nodesJson}};
const CONN_ROWS = {{connectionsJson}};
const REPORT_LABELS = {{labelsJson}};
</script>
<script>
(function() {
  'use strict';

  var COLLAPSE_STATE_KEY = 'dbw-report-collapse-v1';

  function L(key, fallback) {
    if (REPORT_LABELS && Object.prototype.hasOwnProperty.call(REPORT_LABELS, key) && REPORT_LABELS[key]) {
      return String(REPORT_LABELS[key]);
    }

    return fallback;
  }

  function formatText(template, values) {
    return String(template).replace(/\{(\d+)\}/g, function(_, index) {
      var parsedIndex = parseInt(index, 10);
      return Number.isFinite(parsedIndex) && parsedIndex < values.length ? String(values[parsedIndex]) : '';
    });
  }

  function applyStaticLocalization() {
    document.querySelectorAll('[data-i18n]').forEach(function(element) {
      var key = element.getAttribute('data-i18n');
      if (!key) {
        return;
      }

      element.textContent = L(key, element.textContent || '');
    });

    document.querySelectorAll('[data-i18n-placeholder]').forEach(function(element) {
      var key = element.getAttribute('data-i18n-placeholder');
      if (!key) {
        return;
      }

      element.setAttribute('placeholder', L(key, element.getAttribute('placeholder') || ''));
    });

    document.querySelectorAll('[data-i18n-aria-label]').forEach(function(element) {
      var key = element.getAttribute('data-i18n-aria-label');
      if (!key) {
        return;
      }

      element.setAttribute('aria-label', L(key, element.getAttribute('aria-label') || ''));
    });
  }

  function escHtml(s) {
    return String(s)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  function statusClass(status) {
    if (status === 'error') return 'status-error';
    if (status === 'warning') return 'status-warning';
    return 'status-success';
  }

  var tableState = {
    results: { page: 1, pageSize: 10, sortKey: '', sortDir: 'asc', filter: '', filters: [], visibleColumns: {} },
    nodes: { page: 1, pageSize: 10, sortKey: 'category', sortDir: 'asc', filter: '', filters: [], visibleColumns: {} },
    conns: { page: 1, pageSize: 10, sortKey: 'fromNode', sortDir: 'asc', filter: '', filters: [], visibleColumns: {} },
    metadata: { page: 1, pageSize: 10, sortKey: 'field', sortDir: 'asc', filter: '', filters: [], visibleColumns: {} }
  };

  function ensureVisibleColumns(stateKey, columns) {
    var state = tableState[stateKey];
    if (!state.visibleColumns) {
      state.visibleColumns = {};
    }

    columns.forEach(function(col) {
      if (typeof state.visibleColumns[col.key] === 'undefined') {
        state.visibleColumns[col.key] = true;
      }
    });

    Object.keys(state.visibleColumns).forEach(function(key) {
      var exists = columns.some(function(col) { return col.key === key; });
      if (!exists) {
        delete state.visibleColumns[key];
      }
    });

    if (columns.length > 0 && !columns.some(function(col) { return state.visibleColumns[col.key] !== false; })) {
      state.visibleColumns[columns[0].key] = true;
    }
  }

  function getVisibleColumns(stateKey, columns) {
    ensureVisibleColumns(stateKey, columns);
    var state = tableState[stateKey];
    return columns.filter(function(col) { return state.visibleColumns[col.key] !== false; });
  }

  function textValue(value) {
    if (value == null) {
      return '';
    }

    return String(value);
  }

  function compareValues(a, b) {
    var aText = textValue(a).toLowerCase();
    var bText = textValue(b).toLowerCase();

    if (aText < bText) {
      return -1;
    }

    if (aText > bText) {
      return 1;
    }

    return 0;
  }

  function parseDateKey(value) {
    if (value === null || value === undefined || value === '') {
      return null;
    }

    var date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return null;
    }

    return [
      String(date.getFullYear()).padStart(4, '0'),
      String(date.getMonth() + 1).padStart(2, '0'),
      String(date.getDate()).padStart(2, '0')
    ].join('-');
  }

  function rowKindForColumn(row, columns, key) {
    if (key === 'value' && row && row.kind) {
      return row.kind;
    }

    for (var i = 0; i < columns.length; i += 1) {
      if (columns[i].key === key && columns[i].kind) {
        return columns[i].kind;
      }
    }

    var sample = row ? row[key] : null;
    return valueKind(sample);
  }

  function matchFilterValue(left, op, right, kind) {
    if (kind === 'date') {
      var leftDate = parseDateKey(left);
      var rightDate = parseDateKey(right);

      if (leftDate === null || rightDate === null) {
        return op === '!=' ? leftDate !== rightDate : false;
      }

      if (op === '=') return leftDate === rightDate;
      if (op === '!=') return leftDate !== rightDate;
      if (op === '>') return leftDate > rightDate;
      if (op === '<') return leftDate < rightDate;
      if (op === '>=') return leftDate >= rightDate;
      if (op === '<=') return leftDate <= rightDate;
      return leftDate.indexOf(rightDate) >= 0;
    }

    if (kind === 'number') {
      var leftNumber = Number(left);
      var rightNumber = Number(right);
      if (Number.isNaN(leftNumber) || Number.isNaN(rightNumber)) {
        return op === '!=' ? String(left) !== String(right) : false;
      }

      if (op === '=') return leftNumber === rightNumber;
      if (op === '!=') return leftNumber !== rightNumber;
      if (op === '>') return leftNumber > rightNumber;
      if (op === '<') return leftNumber < rightNumber;
      if (op === '>=') return leftNumber >= rightNumber;
      if (op === '<=') return leftNumber <= rightNumber;
      return String(leftNumber).indexOf(String(rightNumber)) >= 0;
    }

    if (kind === 'bool') {
      var leftBool = String(left).toLowerCase() === 'true';
      var rightBool = String(right).toLowerCase() === 'true';
      if (op === '=') return leftBool === rightBool;
      if (op === '!=') return leftBool !== rightBool;
      return leftBool === rightBool;
    }

    var leftText = textValue(left).toLowerCase();
    var rightText = textValue(right).toLowerCase();

    if (op === '=') return leftText === rightText;
    if (op === '!=') return leftText !== rightText;
    if (op === 'contains') return leftText.indexOf(rightText) >= 0;
    if (op === '>') return leftText > rightText;
    if (op === '<') return leftText < rightText;
    if (op === '>=') return leftText >= rightText;
    if (op === '<=') return leftText <= rightText;
    return leftText.indexOf(rightText) >= 0;
  }

  function matchRowFilter(row, filter, columns) {
    var kind = rowKindForColumn(row, columns, filter.col);
    return matchFilterValue(row[filter.col], filter.op, filter.val, kind);
  }

  function applyFilterSortPage(rows, state, searchableKeys) {
    var filtered = rows;
    if (state.filters && state.filters.length > 0) {
      filtered = rows.filter(function(row) {
        return state.filters.every(function(filter) {
          return matchRowFilter(row, filter, state.columns || []);
        });
      });
    }

    if (state.filter) {
      var needle = state.filter.toLowerCase();
      filtered = filtered.filter(function(row) {
        return searchableKeys.some(function(key) {
          return textValue(row[key]).toLowerCase().indexOf(needle) >= 0;
        });
      });
    }

    filtered = filtered.slice().sort(function(left, right) {
      var cmp = compareValues(left[state.sortKey], right[state.sortKey]);
      return state.sortDir === 'asc' ? cmp : -cmp;
    });

    var total = filtered.length;
    var pageCount = Math.max(1, Math.ceil(total / state.pageSize));
    state.page = Math.min(state.page, pageCount);
    var startIndex = (state.page - 1) * state.pageSize;
    var paged = filtered.slice(startIndex, startIndex + state.pageSize);

    return {
      rows: paged,
      total: total,
      pageCount: pageCount,
      start: total === 0 ? 0 : startIndex + 1,
      end: total === 0 ? 0 : Math.min(startIndex + state.pageSize, total)
    };
  }

  function renderTable(containerId, columns, rawRows, stateKey, searchableKeys) {
    var container = document.getElementById(containerId);
    var state = tableState[stateKey];
    state.columns = columns;
    var visibleColumns = getVisibleColumns(stateKey, columns);
    renderColumnSelector(stateKey);

    if (visibleColumns.length === 0) {
      container.innerHTML = '<div class="tbl-wrap"><table><thead><tr><th>' + escHtml(L('table_columns', 'Columns')) + '</th></tr></thead><tbody><tr><td>' + escHtml(L('table_select_one_column', 'Select at least one column.')) + '</td></tr></tbody></table></div>';
      return;
    }

    var result = applyFilterSortPage(rawRows, state, searchableKeys);

    if (result.total === 0) {
      container.innerHTML = '<div class="tbl-wrap"><table><thead><tr>'
        + visibleColumns.map(function(col) { return '<th data-key="' + escHtml(col.key) + '">' + escHtml(col.label) + '</th>'; }).join('')
        + '</tr></thead><tbody><tr><td colspan="' + visibleColumns.length + '">' + escHtml(L('table_no_rows', 'No rows available.')) + '</td></tr></tbody></table></div>';
      return;
    }

    var header = visibleColumns.map(function(col) {
      var css = '';
      if (state.sortKey === col.key) {
        css = state.sortDir === 'asc' ? 'sorted-asc' : 'sorted-desc';
      }

      return '<th class="' + css + '" data-key="' + escHtml(col.key) + '">' + escHtml(col.label) + '</th>';
    }).join('');

    var rowsHtml = result.rows.map(function(row) {
      return '<tr>' + visibleColumns.map(function(col) {
        var cellValue = row[col.key];
        var kind = rowKindForColumn(row, visibleColumns, col.key);
        return '<td class="' + valueClass(kind, cellValue) + '">' + escHtml(formatEmpty(cellValue)) + '</td>';
      }).join('') + '</tr>';
    }).join('');

    var prevDisabled = state.page <= 1 ? 'disabled' : '';
    var nextDisabled = state.page >= result.pageCount ? 'disabled' : '';
    var pageButtonsHtml = buildPageButtons(state.page, result.pageCount).map(function(entry) {
      if (entry == null) {
        return '<span>...</span>';
      }

      var isActive = entry === state.page ? 'active' : '';
      return '<button type="button" class="' + isActive + '" data-page-num="' + entry + '" data-table="' + stateKey + '">' + entry + '</button>';
    }).join('');

    container.innerHTML = '<div class="tbl-wrap"><table><thead><tr>' + header + '</tr></thead><tbody>' + rowsHtml + '</tbody></table></div>'
      + '<div class="pager"><div class="pager-info">' + escHtml(formatText(L('pager_showing_of', 'Showing {0}-{1} of {2}'), [result.start, result.end, result.total])) + '</div>'
      + '<div class="pager-btns"><button type="button" data-page-action="prev" data-table="' + stateKey + '" ' + prevDisabled + '>' + escHtml(L('pager_prev', 'Prev')) + '</button>'
      + pageButtonsHtml
      + '<button type="button" data-page-action="next" data-table="' + stateKey + '" ' + nextDisabled + '>' + escHtml(L('pager_next', 'Next')) + '</button></div></div>';

    container.querySelectorAll('th[data-key]').forEach(function(th) {
      th.addEventListener('click', function() {
        var key = th.getAttribute('data-key');
        if (state.sortKey === key) {
          state.sortDir = state.sortDir === 'asc' ? 'desc' : 'asc';
        } else {
          state.sortKey = key;
          state.sortDir = 'asc';
        }

        state.page = 1;
        rerenderTable(stateKey);
      });
    });

    container.querySelectorAll('button[data-page-action]').forEach(function(button) {
      button.addEventListener('click', function() {
        if (button.hasAttribute('disabled')) {
          return;
        }

        var action = button.getAttribute('data-page-action');
        if (action === 'prev' && state.page > 1) {
          state.page -= 1;
          rerenderTable(stateKey);
        }

        if (action === 'next' && state.page < result.pageCount) {
          state.page += 1;
          rerenderTable(stateKey);
        }
      });
    });

    container.querySelectorAll('button[data-page-num]').forEach(function(button) {
      button.addEventListener('click', function() {
        var parsedPage = parseInt(button.getAttribute('data-page-num') || '', 10);
        if (!Number.isFinite(parsedPage) || parsedPage < 1 || parsedPage > result.pageCount) {
          return;
        }

        if (parsedPage === state.page) {
          return;
        }

        state.page = parsedPage;
        rerenderTable(stateKey);
      });
    });
  }

  function tableFilterFields(stateKey) {
    if (stateKey === 'results') {
      var cols = tableState.results.columns || [];
      return cols.map(function(col) {
        return { key: col.key, label: col.label };
      });
    }

    if (stateKey === 'nodes') {
      return [
        { key: 'category', label: L('field_category', 'Category') },
        { key: 'type', label: L('field_type', 'Type') },
        { key: 'title', label: L('field_title', 'Title') },
        { key: 'status', label: L('field_status', 'Status') }
      ];
    }

    if (stateKey === 'conns') {
      return [
        { key: 'fromNode', label: L('field_from_node', 'From Node') },
        { key: 'fromPin', label: L('field_from_pin', 'From Pin') },
        { key: 'toNode', label: L('field_to_node', 'To Node') },
        { key: 'toPin', label: L('field_to_pin', 'To Pin') },
        { key: 'dataType', label: L('field_data_type', 'Data Type') }
      ];
    }

    if (stateKey === 'metadata') {
      return [
        { key: 'field', label: L('field_field', 'Field') },
        { key: 'label', label: L('field_label', 'Label') },
        { key: 'value', label: L('field_value', 'Value') },
        { key: 'kind', label: L('field_type', 'Type') }
      ];
    }

    return [];
  }

  function filterFieldLabel(stateKey, key) {
    var fields = tableFilterFields(stateKey);
    for (var i = 0; i < fields.length; i += 1) {
      if (fields[i].key === key) {
        return fields[i].label;
      }
    }

    return key;
  }

  function setupFilterFieldSelect(stateKey, fieldSelect) {
    if (!fieldSelect) {
      return;
    }

    fieldSelect.innerHTML = '';
    tableFilterFields(stateKey).forEach(function(field) {
      fieldSelect.add(new Option(field.label, field.key));
    });
  }

  function renderFilterChips(stateKey, listId) {
    var list = document.getElementById(listId);
    if (!list) {
      return;
    }

    var state = tableState[stateKey];
    list.innerHTML = '';
    if (!state.filters.length) {
      list.innerHTML = '<div class="acc-empty">' + escHtml(L('filters_none_active', 'No filters active.')) + '</div>';
      return;
    }

    state.filters.forEach(function(filter, index) {
      var kind = rowKindForColumn(null, state.columns || [], filter.col);
      var chip = document.createElement('div');
      chip.className = 'filter-chip';
      chip.innerHTML =
        '<span class="chip-col">' + escHtml(filterFieldLabel(stateKey, filter.col)) + '</span>' +
        '<span class="chip-op">' + escHtml(filterOpLabel(filter.op)) + '</span>' +
        '<span class="chip-val">' + escHtml(formatFilterValue(filter.val, kind)) + '</span>' +
        '<button class="chip-rm" type="button" data-filter-index="' + index + '" title="' + escHtml(L('button_remove', 'Remove')) + '">×</button>';
      list.appendChild(chip);
    });

    list.querySelectorAll('button[data-filter-index]').forEach(function(button) {
      button.addEventListener('click', function() {
        var idx = parseInt(button.getAttribute('data-filter-index') || '', 10);
        if (!Number.isFinite(idx)) {
          return;
        }

        state.filters.splice(idx, 1);
        renderFilterChips(stateKey, listId);
        rerenderTable(stateKey);
      });
    });
  }

  function renderColumnSelector(stateKey) {
    var list = document.getElementById(stateKey + '-column-list');
    if (!list) {
      return;
    }

    var state = tableState[stateKey];
    var columns = state.columns || [];
    if (!columns.length) {
      list.innerHTML = '<div class="acc-empty">' + escHtml(L('columns_none_available', 'No columns available.')) + '</div>';
      return;
    }

    ensureVisibleColumns(stateKey, columns);
    list.innerHTML = '';

    columns.forEach(function(col) {
      var chip = document.createElement('label');
      chip.className = 'filter-chip';
      chip.innerHTML = '<input type="checkbox" data-col-key="' + escHtml(col.key) + '" ' + (state.visibleColumns[col.key] !== false ? 'checked' : '') + '/> '
        + '<span class="chip-col">' + escHtml(col.label) + '</span>';
      list.appendChild(chip);
    });

    list.querySelectorAll('input[data-col-key]').forEach(function(input) {
      input.addEventListener('change', function() {
        var key = input.getAttribute('data-col-key');
        if (!key) {
          return;
        }

        state.visibleColumns[key] = input.checked;
        if (!Object.keys(state.visibleColumns).some(function(colKey) { return state.visibleColumns[colKey] !== false; })) {
          state.visibleColumns[key] = true;
          input.checked = true;
        }

        rerenderTable(stateKey);
      });
    });
  }

  function initFilterControls(stateKey, config) {
    var state = tableState[stateKey];
    var fieldSelect = document.getElementById(config.fieldSelectId);
    var opSelect = document.getElementById(config.operatorSelectId);
    var valueInput = document.getElementById(config.valueInputId);
    var addButton = document.getElementById(config.addButtonId);
    var clearButton = document.getElementById(config.clearButtonId);

    if (!fieldSelect || !opSelect || !valueInput || !addButton || !clearButton) {
      return;
    }

    setupFilterFieldSelect(stateKey, fieldSelect);

    function updateInputType() {
      var kind = rowKindForColumn(null, state.columns || [], fieldSelect.value);
      valueInput.type = kind === 'date' ? 'date' : 'text';
      valueInput.placeholder = kind === 'date' ? L('filter_select_date', 'Select a date') : L('filter_value_placeholder', 'Filter value');
    }

    fieldSelect.addEventListener('change', updateInputType);

    addButton.addEventListener('click', function() {
      var rawValue = valueInput.value;
      if (!rawValue) {
        return;
      }

      var kind = rowKindForColumn(null, state.columns || [], fieldSelect.value);
      state.filters.push({
        col: fieldSelect.value,
        op: opSelect.value,
        val: kind === 'date' ? rawValue.slice(0, 10) : rawValue
      });

      valueInput.value = '';
      state.page = 1;
      renderFilterChips(stateKey, config.filterListId);
      rerenderTable(stateKey);
    });

    clearButton.addEventListener('click', function() {
      state.filters = [];
      state.page = 1;
      renderFilterChips(stateKey, config.filterListId);
      rerenderTable(stateKey);
    });

    valueInput.addEventListener('keydown', function(e) {
      if (e.key === 'Enter') {
        e.preventDefault();
        addButton.click();
      }
    });

    updateInputType();
    renderFilterChips(stateKey, config.filterListId);
  }

  function buildPageButtons(currentPage, pageCount) {
    if (pageCount <= 7) {
      var allPages = [];
      for (var i = 1; i <= pageCount; i += 1) {
        allPages.push(i);
      }

      return allPages;
    }

    var pages = [1];
    var start = Math.max(2, currentPage - 1);
    var end = Math.min(pageCount - 1, currentPage + 1);

    if (currentPage <= 3) {
      start = 2;
      end = 4;
    }

    if (currentPage >= pageCount - 2) {
      start = pageCount - 3;
      end = pageCount - 1;
    }

    if (start > 2) {
      pages.push(null);
    }

    for (var page = start; page <= end; page += 1) {
      pages.push(page);
    }

    if (end < pageCount - 1) {
      pages.push(null);
    }

    pages.push(pageCount);
    return pages;
  }

  function debounce(action, waitMs) {
    var timeoutId = null;

    return function(value) {
      if (timeoutId !== null) {
        clearTimeout(timeoutId);
      }

      timeoutId = setTimeout(function() {
        action(value);
      }, waitMs);
    };
  }

  function rerenderTable(stateKey) {
    if (stateKey === 'results') {
      renderResults();
      return;
    }

    if (stateKey === 'nodes') {
      renderNodes();
      return;
    }

    if (stateKey === 'conns') {
      renderConnections();
      return;
    }

    if (stateKey === 'metadata') {
      renderMetadata();
    }
  }

  function formatEmpty(value) {
    if (value === null || value === undefined || value === '') {
      return REPORT_META.useDashForEmptyFields ? '-' : 'null';
    }

    return String(value);
  }

  function valueKind(value) {
    if (value === null || value === undefined || value === '') {
      return 'null';
    }

    if (typeof value === 'boolean') {
      return 'bool';
    }

    if (typeof value === 'number') {
      return 'number';
    }

    if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}(?:[T\s].*)?$/.test(value)) {
      return 'date';
    }

    return 'text';
  }

  function valueClass(kind, value) {
    if (kind === 'null') return 'type-null';
    if (kind === 'bool') return value ? 'type-success' : 'type-error';
    if (kind === 'number') return 'type-number';
    if (kind === 'date') return 'type-date';
    return 'type-text';
  }

  function renderSummary() {
    var body = document.getElementById('summary-body');
    var summary = REPORT_META.summary || {};
    var cards = [
      { label: L('summary_status', 'Status'), value: summary.status || 'success', hint: L('summary_hint_execution_outcome', 'Execution outcome'), className: statusClass(summary.status || 'success') },
      { label: L('summary_execution_time', 'Execution Time'), value: summary.executionTimeMs == null ? null : (String(summary.executionTimeMs) + ' ms'), hint: L('summary_hint_measured_duration', 'Measured duration'), className: 'type-number' },
      { label: L('summary_rows', 'Rows'), value: summary.rowCount, hint: L('summary_hint_returned_rows', 'Returned rows'), className: 'type-number' },
      { label: L('summary_columns', 'Columns'), value: REPORT_META.columnCount, hint: L('summary_hint_detected_columns', 'Detected output columns'), className: 'type-number' },
    ];

    var html = '<div class="kpi-grid">' + cards.map(function(card) {
      var className = card.className ? ' ' + card.className : '';
      return '<div class="kpi-card">'
        + '<div class="label">' + escHtml(card.label) + '</div>'
        + '<div class="value' + className + '">' + escHtml(formatEmpty(card.value)) + '</div>'
        + '<div class="hint">' + escHtml(card.hint) + '</div>'
        + '</div>';
    }).join('') + '</div>';

    if (summary.errorMessage) {
      html += '<div class="banner banner-err"><strong>' + escHtml(L('summary_error_prefix', 'Error:')) + '</strong> ' + escHtml(summary.errorMessage) + '</div>';
    }

    body.innerHTML = html;
  }

  function renderMetadataRows() {
        return REPORT_META.metadata || [];
      }

      function filterOpLabel(op) {
        var labels = {
          '=': '=',
          '!=': '≠',
          'contains': '⊃',
          '>': '>',
          '<': '<',
          '>=': '≥',
          '<=': '≤'
        };

        return labels[op] || op;
      }

      function formatFilterValue(value, kind) {
        if (kind === 'date') {
          return value ? String(value).slice(0, 10) : formatEmpty(value);
        }

        return formatEmpty(value);
      }

      function renderMetadataFilters() {
        var list = document.getElementById('metadata-filter-list');
        var state = tableState.metadata;

        list.innerHTML = '';
        if (!state.filters.length) {
          list.innerHTML = '<div class="acc-empty">' + escHtml(L('filters_metadata_none_active', 'No metadata filters active.')) + '</div>';
          return;
        }

        state.filters.forEach(function(filter, index) {
          var kind = metadataValueKindForField(filter.col);
          var chip = document.createElement('div');
          chip.className = 'filter-chip';
          chip.innerHTML =
            '<span class="chip-col">' + escHtml(filterFieldLabel('metadata', filter.col)) + '</span>' +
            '<span class="chip-op">' + escHtml(filterOpLabel(filter.op)) + '</span>' +
            '<span class="chip-val">' + escHtml(formatFilterValue(filter.val, kind)) + '</span>' +
            '<button class="chip-rm" type="button" data-metadata-filter-index="' + index + '" title="' + escHtml(L('button_remove', 'Remove')) + '">×</button>';
          list.appendChild(chip);
        });

        list.querySelectorAll('button[data-metadata-filter-index]').forEach(function(button) {
          button.addEventListener('click', function() {
            var index = parseInt(button.getAttribute('data-metadata-filter-index') || '', 10);
            if (!Number.isFinite(index)) {
              return;
            }

            removeMetadataFilter(index);
          });
        });
      }

      function renderMetadata() {
        var section = document.getElementById('s-meta');
        var rows = renderMetadataRows();

        if (!REPORT_META.includeMetadata) {
          section.style.display = 'none';
          return;
        }

        section.style.display = '';

        renderTable(
          'metadata-table',
          [
            { key: 'field', label: L('field_field', 'Field'), kind: 'text' },
            { key: 'label', label: L('field_label', 'Label'), kind: 'text' },
            { key: 'value', label: L('field_value', 'Value') },
            { key: 'kind', label: L('field_type', 'Type'), kind: 'text' }
          ],
          rows,
          'metadata',
          ['field', 'label', 'value', 'kind']);

        renderMetadataFilters();
      }

      function metadataFilterFields() {
        return tableFilterFields('metadata');
      }

      function metadataValueKindForField(field) {
        if (field === 'value' && renderMetadataRows().some(function(row) { return row.kind === 'date'; })) {
          return 'date';
        }

        return 'text';
      }

      function updateMetadataFilterInput() {
        var fieldSelect = document.getElementById('metadata-filter-col');
        var valueInput = document.getElementById('metadata-filter-val');
        if (!fieldSelect || !valueInput) {
          return;
        }

        var kind = metadataValueKindForField(fieldSelect.value);
        valueInput.type = kind === 'date' ? 'date' : 'text';
        valueInput.placeholder = kind === 'date' ? L('filter_select_date', 'Select a date') : L('filter_value_placeholder', 'Filter value');
      }

      function initMetadataFilterControls() {
        var fieldSelect = document.getElementById('metadata-filter-col');
        var opSelect = document.getElementById('metadata-filter-op');
        var valueInput = document.getElementById('metadata-filter-val');
        var addButton = document.getElementById('metadata-add-filter');
        var clearButton = document.getElementById('metadata-clear-filters');

        if (!fieldSelect || !opSelect || !valueInput || !addButton || !clearButton) {
          return;
        }

        fieldSelect.innerHTML = '';
        metadataFilterFields().forEach(function(field) {
          fieldSelect.add(new Option(field.label, field.key));
        });

        fieldSelect.addEventListener('change', updateMetadataFilterInput);

        addButton.addEventListener('click', function() {
          var state = tableState.metadata;
          var rawValue = valueInput.value;
          var kind = metadataValueKindForField(fieldSelect.value);
          if (!rawValue) {
            return;
          }

          state.filters.push({
            col: fieldSelect.value,
            op: opSelect.value,
            val: kind === 'date' ? rawValue.slice(0, 10) : rawValue
          });

          valueInput.value = '';
          renderMetadataFilters();
          rerenderTable('metadata');
        });

        clearButton.addEventListener('click', function() {
          var state = tableState.metadata;
          state.filters = [];
          renderMetadataFilters();
          rerenderTable('metadata');
        });

        valueInput.addEventListener('keydown', function(e) {
          if (e.key === 'Enter') {
            e.preventDefault();
            addButton.click();
          }
        });

        updateMetadataFilterInput();
      }

      function removeMetadataFilter(index) {
        var state = tableState.metadata;
        state.filters.splice(index, 1);
        renderMetadataFilters();
        rerenderTable('metadata');
      }

  function renderSql() {
    var sqlPre = document.getElementById('sql-pre');
    sqlPre.textContent = HAS_SQL ? SQL_TEXT : L('sql_not_available', '-- SQL not available.');
  }

  function renderQuality() {
    var body = document.getElementById('quality-body');
    var status = EXECUTION_RESULT.status || 'success';
    var cssClass = status === 'error' ? 'banner-err' : (status === 'warning' ? 'banner-warn' : 'banner-ok');
    var summary = status === 'error'
      ? L('quality_generated_errors', 'Generated with errors')
      : (status === 'warning' ? L('quality_generated_warnings', 'Generated with warnings') : L('quality_generated_success', 'Query generated successfully'));

      var lines = [
        '<div class="banner ' + cssClass + '"><strong>' + escHtml(summary) + '</strong></div>',
        '<div class="meta-grid" style="margin-top:12px">',
        '<div class="meta-card"><div class="label">' + escHtml(L('quality_status', 'Status')) + '</div><div class="value ' + statusClass(status) + '">' + escHtml(status) + '</div></div>',
        '<div class="meta-card"><div class="label">' + escHtml(L('quality_error_count', 'Error Count')) + '</div><div class="value">' + escHtml(String(REPORT_META.errorCount || 0)) + '</div></div>',
        '<div class="meta-card"><div class="label">' + escHtml(L('quality_warning_count', 'Warning Count')) + '</div><div class="value">' + escHtml(String(REPORT_META.warningCount || 0)) + '</div></div>',
        '</div>'
      ];

      if (EXECUTION_RESULT.errorMessage) {
        lines.push('<div class="banner banner-err"><strong>' + escHtml(L('quality_error_message', 'Error Message:')) + '</strong> ' + escHtml(EXECUTION_RESULT.errorMessage) + '</div>');
      }

      body.innerHTML = lines.join('');
  }

  function renderResults() {
    var section = document.getElementById('s-results');
    var body = document.getElementById('results-body');
    if (!body || !section) {
      return;
    }

    if (!RESULT_ROWS || !Array.isArray(RESULT_ROWS) || RESULT_ROWS.length === 0) {
      body.innerHTML = '<span>' + escHtml(L('results_no_rows', 'No query rows available.')) + '</span>';
      return;
    }

    var columns = [];
    if (RESULT_ROWS.length > 0) {
      Object.keys(RESULT_ROWS[0]).forEach(function(key) {
        columns.push({
          key: key,
          label: String(key).replace(/_/g, ' ')
        });
      });
    }

    if (columns.length === 0) {
      body.innerHTML = '<span>' + escHtml(L('results_no_rows', 'No query rows available.')) + '</span>';
      return;
    }

    var state = tableState.results;
    if (!state.sortKey) {
      state.sortKey = columns[0].key;
    }

    state.columns = columns;

    renderTable(
      'results-body',
      columns,
      RESULT_ROWS,
      'results',
      columns.map(function(col) { return col.key; }));

    renderFilterChips('results', 'results-filter-list');
  }

  function renderNodes() {
    var section = document.getElementById('s-nodes');
    var body = document.getElementById('nodes-body');
    if (!section || !body) {
      return;
    }

    if (!REPORT_META.includeMetadata) {
      section.style.display = 'none';
      return;
    }

    section.style.display = '';

    if (!NODE_ROWS || !Array.isArray(NODE_ROWS) || NODE_ROWS.length === 0) {
      body.innerHTML = '<span>' + escHtml(L('nodes_no_details', 'No node details available for SQL editor exports.')) + '</span>';
      return;
    }

    renderTable(
      'nodes-body',
      [
        { key: 'category', label: L('field_category', 'Category') },
        { key: 'type', label: L('field_type', 'Type') },
        { key: 'title', label: L('field_title', 'Title') },
        { key: 'status', label: L('field_status', 'Status') }
      ],
      NODE_ROWS,
      'nodes',
      ['category', 'type', 'title', 'status']);

    renderFilterChips('nodes', 'nodes-filter-list');
  }

  function renderConnections() {
    var section = document.getElementById('s-conns');
    var body = document.getElementById('conns-body');
    if (!section || !body) {
      return;
    }

    if (!REPORT_META.includeMetadata) {
      section.style.display = 'none';
      return;
    }

    section.style.display = '';

    if (!CONN_ROWS || !Array.isArray(CONN_ROWS) || CONN_ROWS.length === 0) {
      body.innerHTML = '<span>' + escHtml(L('connections_no_details', 'No connection details available for SQL editor exports.')) + '</span>';
      return;
    }

    renderTable(
      'conns-body',
      [
        { key: 'fromNode', label: L('field_from_node', 'From Node') },
        { key: 'fromPin', label: L('field_from_pin', 'From Pin') },
        { key: 'toNode', label: L('field_to_node', 'To Node') },
        { key: 'toPin', label: L('field_to_pin', 'To Pin') },
        { key: 'dataType', label: L('field_data_type', 'Data Type') }
      ],
      CONN_ROWS,
      'conns',
      ['fromNode', 'fromPin', 'toNode', 'toPin', 'dataType']);

    renderFilterChips('conns', 'conns-filter-list');
  }

  function bindTableControls(searchId, pageSizeId, stateKey) {
    var search = document.getElementById(searchId);
    var pageSize = document.getElementById(pageSizeId);
    var state = tableState[stateKey];

    if (search) {
      var updateFilter = debounce(function(rawValue) {
        state.filter = rawValue || '';
        state.page = 1;
        rerenderTable(stateKey);
      }, 120);

      search.addEventListener('input', function() {
        updateFilter(search.value || '');
      });
    }

    if (pageSize) {
      pageSize.addEventListener('change', function() {
        var parsed = parseInt(pageSize.value, 10);
        state.pageSize = Number.isFinite(parsed) && parsed > 0 ? parsed : 10;
        state.page = 1;
        rerenderTable(stateKey);
      });
    }
  }

  function initOrderControls(stateKey, config) {
    var state = tableState[stateKey];
    var fieldSelect = document.getElementById(config.fieldSelectId);
    var dirSelect = document.getElementById(config.directionSelectId);
    var applyButton = document.getElementById(config.applyButtonId);
    var resetButton = document.getElementById(config.resetButtonId);

    if (!fieldSelect || !dirSelect || !applyButton || !resetButton || !state) {
      return;
    }

    function refreshOrderFields() {
      var fields = tableFilterFields(stateKey);
      fieldSelect.innerHTML = '';

      fields.forEach(function(field) {
        fieldSelect.add(new Option(field.label, field.key));
      });

      if (!fields.length) {
        fieldSelect.disabled = true;
        dirSelect.disabled = true;
        applyButton.setAttribute('disabled', 'disabled');
        resetButton.setAttribute('disabled', 'disabled');
        return;
      }

      fieldSelect.disabled = false;
      dirSelect.disabled = false;
      applyButton.removeAttribute('disabled');
      resetButton.removeAttribute('disabled');

      var hasCurrentSort = fields.some(function(field) { return field.key === state.sortKey; });
      if (!hasCurrentSort) {
        state.sortKey = fields[0].key;
      }

      fieldSelect.value = state.sortKey;
      dirSelect.value = state.sortDir || 'asc';
    }

    applyButton.addEventListener('click', function() {
      if (!fieldSelect.value) {
        return;
      }

      state.sortKey = fieldSelect.value;
      state.sortDir = dirSelect.value === 'desc' ? 'desc' : 'asc';
      state.page = 1;
      rerenderTable(stateKey);
    });

    resetButton.addEventListener('click', function() {
      refreshOrderFields();
      state.sortDir = 'asc';
      if (fieldSelect.value) {
        state.sortKey = fieldSelect.value;
      }

      dirSelect.value = 'asc';
      state.page = 1;
      rerenderTable(stateKey);
    });

    refreshOrderFields();
  }

  function initSubpanelCollapsibles() {
    function setSubpanelButtonState(button, isCollapsed) {
      button.textContent = isCollapsed
        ? ('▸ ' + L('button_expand', 'Expand'))
        : ('▾ ' + L('button_collapse', 'Collapse'));
    }

    document.querySelectorAll('[data-subcollapse-target]').forEach(function(button) {
      var target = button.getAttribute('data-subcollapse-target');
      if (!target) {
        return;
      }

      var panel = document.getElementById(target);
      if (!panel) {
        return;
      }

      var isCollapsed = panel.classList.contains('is-collapsed');
      button.setAttribute('aria-expanded', isCollapsed ? 'false' : 'true');
      setSubpanelButtonState(button, isCollapsed);

      button.addEventListener('click', function() {
        panel.classList.toggle('is-collapsed');
        var collapsed = panel.classList.contains('is-collapsed');
        button.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
        setSubpanelButtonState(button, collapsed);
      });
    });
  }

  function initColumnControls(stateKey) {
    var allButton = document.getElementById(stateKey + '-columns-all');
    var noneButton = document.getElementById(stateKey + '-columns-none');
    var resetButton = document.getElementById(stateKey + '-columns-reset');
    if (!allButton || !noneButton || !resetButton) {
      return;
    }

    allButton.addEventListener('click', function() {
      var state = tableState[stateKey];
      (state.columns || []).forEach(function(col) { state.visibleColumns[col.key] = true; });
      rerenderTable(stateKey);
    });

    noneButton.addEventListener('click', function() {
      var state = tableState[stateKey];
      var columns = state.columns || [];
      columns.forEach(function(col) { state.visibleColumns[col.key] = false; });
      if (columns.length > 0) {
        state.visibleColumns[columns[0].key] = true;
      }
      rerenderTable(stateKey);
    });

    resetButton.addEventListener('click', function() {
      var state = tableState[stateKey];
      state.visibleColumns = {};
      rerenderTable(stateKey);
    });
  }

  function initCollapsibles() {
    function loadState() {
      try {
        var raw = localStorage.getItem(COLLAPSE_STATE_KEY);
        if (!raw) {
          return {};
        }

        var parsed = JSON.parse(raw);
        return parsed && typeof parsed === 'object' ? parsed : {};
      } catch (_) {
        return {};
      }
    }

    function saveState(state) {
      try {
        localStorage.setItem(COLLAPSE_STATE_KEY, JSON.stringify(state));
      } catch (_) {
      }
    }

    function setCollapseButtonState(button, isCollapsed) {
      button.textContent = isCollapsed
        ? ('▸ ' + L('button_expand', 'Expand'))
        : ('▾ ' + L('button_collapse', 'Collapse'));
      if (isCollapsed) {
        button.classList.add('is-collapsed');
      } else {
        button.classList.remove('is-collapsed');
      }
    }

    var state = loadState();

    document.querySelectorAll('[data-collapse-target]').forEach(function(button) {
      var target = button.getAttribute('data-collapse-target');
      if (!target) {
        return;
      }

      var section = document.getElementById(target);
      if (!section) {
        return;
      }

      var isDataSection = target === 's-results';
      if (!isDataSection) {
        section.classList.add('is-collapsed');
      } else if (state[target] === true) {
        section.classList.add('is-collapsed');
      }

      setCollapseButtonState(button, section.classList.contains('is-collapsed'));

      button.addEventListener('click', function() {
        section.classList.toggle('is-collapsed');
        var isCollapsed = section.classList.contains('is-collapsed');
        setCollapseButtonState(button, isCollapsed);
        state[target] = isCollapsed;
        saveState(state);
      });
    });
  }

  function bindFilterKeyboardShortcuts() {
    function isTypingTarget(target) {
      if (!target || !(target instanceof HTMLElement)) {
        return false;
      }

      var tag = (target.tagName || '').toLowerCase();
      return tag === 'input' || tag === 'textarea' || target.isContentEditable;
    }

    function isSectionExpanded(id) {
      var section = document.getElementById(id);
      return !!section && !section.classList.contains('is-collapsed') && section.style.display !== 'none';
    }

    function focusField(id) {
      var field = document.getElementById(id);
      if (!field || !(field instanceof HTMLElement)) {
        return;
      }

      field.focus();
      if (field instanceof HTMLInputElement) {
        field.select();
      }
    }

    function focusFirstVisibleSearch() {
      var candidates = [
        { section: 's-results', input: 'results-search' },
        { section: 's-meta', input: 'metadata-search' },
        { section: 's-nodes', input: 'nodes-search' },
        { section: 's-conns', input: 'conns-search' }
      ];

      for (var i = 0; i < candidates.length; i += 1) {
        var item = candidates[i];
        if (!isSectionExpanded(item.section)) {
          continue;
        }

        var input = document.getElementById(item.input);
        if (input && input instanceof HTMLElement) {
          input.focus();
          if (input instanceof HTMLInputElement) {
            input.select();
          }
          return;
        }
      }
    }

    document.addEventListener('keydown', function(e) {
      if (e.key === '/' && !e.ctrlKey && !e.metaKey && !e.altKey && !isTypingTarget(e.target)) {
        e.preventDefault();
        focusFirstVisibleSearch();
        return;
      }

      if (!e.altKey || e.ctrlKey || e.metaKey) {
        return;
      }

      var key = (e.key || '').toLowerCase();
      if (key === 'm') {
        e.preventDefault();
        focusField('metadata-filter-val');
        return;
      }

      if (key === 'r') {
        e.preventDefault();
        focusField('results-filter-val');
        return;
      }

      if (key === 'n') {
        e.preventDefault();
        focusField('nodes-filter-val');
        return;
      }

      if (key === 'c') {
        e.preventDefault();
        focusField('conns-filter-val');
      }
    });
  }

  function copySql() {
    var text = SQL_TEXT || '';
    if (!text) {
      return;
    }

    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text);
      return;
    }

    var temp = document.createElement('textarea');
    temp.value = text;
    document.body.appendChild(temp);
    temp.select();
    document.execCommand('copy');
    document.body.removeChild(temp);
  }

  function updateThemeButton() {
    var btn = document.getElementById('btn-theme');
    var current = document.documentElement.getAttribute('data-theme') || 'dark';
    btn.textContent = current === 'dark' ? L('theme_light', 'Light') : L('theme_dark', 'Dark');
  }

  function toggleTheme() {
    var root = document.documentElement;
    var current = root.getAttribute('data-theme') || 'dark';
    var next = current === 'dark' ? 'light' : 'dark';
    root.setAttribute('data-theme', next);
    localStorage.setItem('dbw-theme', next);
    updateThemeButton();
  }

  function loadTheme() {
    var saved = localStorage.getItem('dbw-theme');
    if (saved === 'light' || saved === 'dark') {
      document.documentElement.setAttribute('data-theme', saved);
    }
    updateThemeButton();
  }

  function initFooter() {
    var footer = document.getElementById('footer-text');
    footer.innerHTML = escHtml(L('footer_generated_by', 'Generated by')) + ' <strong>AkkornStudio</strong> · ' + escHtml(REPORT_META.generatedAt || '—') + ' · v' + escHtml(REPORT_VERSION);
  }

  document.addEventListener('DOMContentLoaded', function() {
    applyStaticLocalization();
    renderSummary();
    renderMetadata();
    renderSql();
    renderQuality();
    renderResults();
    renderNodes();
    renderConnections();
    loadTheme();
    initFooter();
    document.getElementById('btn-theme').addEventListener('click', toggleTheme);
    document.getElementById('btn-copy-sql').addEventListener('click', copySql);
    bindTableControls('results-search', 'results-page-size', 'results');
    bindTableControls('metadata-search', 'metadata-page-size', 'metadata');
    bindTableControls('nodes-search', 'nodes-page-size', 'nodes');
    bindTableControls('conns-search', 'conns-page-size', 'conns');
    initMetadataFilterControls();
    initFilterControls('results', {
      fieldSelectId: 'results-filter-col',
      operatorSelectId: 'results-filter-op',
      valueInputId: 'results-filter-val',
      addButtonId: 'results-add-filter',
      clearButtonId: 'results-clear-filters',
      filterListId: 'results-filter-list'
    });
    initFilterControls('nodes', {
      fieldSelectId: 'nodes-filter-col',
      operatorSelectId: 'nodes-filter-op',
      valueInputId: 'nodes-filter-val',
      addButtonId: 'nodes-add-filter',
      clearButtonId: 'nodes-clear-filters',
      filterListId: 'nodes-filter-list'
    });
    initFilterControls('conns', {
      fieldSelectId: 'conns-filter-col',
      operatorSelectId: 'conns-filter-op',
      valueInputId: 'conns-filter-val',
      addButtonId: 'conns-add-filter',
      clearButtonId: 'conns-clear-filters',
      filterListId: 'conns-filter-list'
    });
    initOrderControls('results', {
      fieldSelectId: 'results-order-col',
      directionSelectId: 'results-order-dir',
      applyButtonId: 'results-apply-order',
      resetButtonId: 'results-reset-order'
    });
    initOrderControls('metadata', {
      fieldSelectId: 'metadata-order-col',
      directionSelectId: 'metadata-order-dir',
      applyButtonId: 'metadata-apply-order',
      resetButtonId: 'metadata-reset-order'
    });
    initOrderControls('nodes', {
      fieldSelectId: 'nodes-order-col',
      directionSelectId: 'nodes-order-dir',
      applyButtonId: 'nodes-apply-order',
      resetButtonId: 'nodes-reset-order'
    });
    initOrderControls('conns', {
      fieldSelectId: 'conns-order-col',
      directionSelectId: 'conns-order-dir',
      applyButtonId: 'conns-apply-order',
      resetButtonId: 'conns-reset-order'
    });
    initCollapsibles();
    initSubpanelCollapsibles();
    initColumnControls('results');
    initColumnControls('metadata');
    initColumnControls('nodes');
    initColumnControls('conns');
    bindFilterKeyboardShortcuts();
  });
})();
</script>
</body>
</html>
""";
    }

  private static string ResolveReportLanguageTag()
  {
    string ui = CultureInfo.CurrentUICulture.Name;
    return ui.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? "pt-BR" : "en";
  }

  private static object BuildReportLabels(string languageTag)
  {
    if (languageTag.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
    {
      return new
      {
        theme_toggle = "Alternar tema",
        keyboard_shortcuts_aria = "Atalhos de teclado",
        shortcuts_title = "Atalhos",
        shortcuts_focus_search = "Focar busca ativa",
        shortcuts_results_filter = "Filtro de resultados",
        shortcuts_metadata_filter = "Filtro de metadados",
        shortcuts_nodes_filter = "Filtro de nos",
        shortcuts_connections_filter = "Filtro de conexoes",
        section_execution_summary = "Resumo da execucao",
        section_validation_summary = "Resumo de validacao",
        section_query_results = "Resultados da consulta",
        section_node_inventory = "Inventario de nos",
        section_connection_map = "Mapa de conexoes",
        section_sql_query = "Consulta SQL",
        section_optional_metadata = "Metadados opcionais",
        badge_primary_output = "Saida principal",
        button_copy_sql = "Copiar SQL",
        button_expand = "Expandir",
        button_collapse = "Recolher",
        filter_results_placeholder = "Filtrar resultados",
        filter_nodes_placeholder = "Filtrar nos",
        filter_connections_placeholder = "Filtrar conexoes",
        filter_metadata_placeholder = "Filtrar metadados",
        subpanel_filters = "Filtros",
        subpanel_orders = "Ordenacoes",
        subpanel_columns = "Colunas",
        filter_operator_contains = "Contem",
        filter_operator_equals = "= Igual",
        filter_operator_not_equal = "!= Diferente",
        filter_operator_greater_than = "> Maior que",
        filter_operator_less_than = "< Menor que",
        filter_operator_greater_or_equal = ">= Maior ou igual",
        filter_operator_less_or_equal = "<= Menor ou igual",
        filter_value_placeholder = "Valor do filtro",
        filter_select_date = "Selecione uma data",
        filter_add = "Adicionar filtro",
        filter_clear = "Limpar filtros",
        order_direction_asc = "Crescente",
        order_direction_desc = "Decrescente",
        order_apply = "Aplicar ordenacao",
        order_reset = "Resetar ordenacao",
        columns_show_all = "Mostrar todas",
        columns_hide_all = "Ocultar todas",
        columns_reset = "Resetar colunas",
        table_columns = "Colunas",
        table_select_one_column = "Selecione ao menos uma coluna.",
        table_no_rows = "Nenhuma linha disponivel.",
        pager_showing_of = "Exibindo {0}-{1} de {2}",
        pager_prev = "Anterior",
        pager_next = "Proxima",
        filters_none_active = "Nenhum filtro ativo.",
        filters_metadata_none_active = "Nenhum filtro de metadados ativo.",
        button_remove = "Remover",
        columns_none_available = "Nenhuma coluna disponivel.",
        field_category = "Categoria",
        field_type = "Tipo",
        field_title = "Titulo",
        field_status = "Status",
        field_from_node = "No de origem",
        field_from_pin = "Pino de origem",
        field_to_node = "No de destino",
        field_to_pin = "Pino de destino",
        field_data_type = "Tipo de dado",
        field_field = "Campo",
        field_label = "Rotulo",
        field_value = "Valor",
        summary_status = "Status",
        summary_hint_execution_outcome = "Resultado da execucao",
        summary_execution_time = "Tempo de execucao",
        summary_hint_measured_duration = "Duracao medida",
        summary_rows = "Linhas",
        summary_hint_returned_rows = "Linhas retornadas",
        summary_columns = "Colunas",
        summary_hint_detected_columns = "Colunas detectadas",
        summary_error_prefix = "Erro:",
        sql_not_available = "-- SQL indisponivel.",
        quality_generated_errors = "Gerado com erros",
        quality_generated_warnings = "Gerado com avisos",
        quality_generated_success = "Consulta gerada com sucesso",
        quality_status = "Status",
        quality_error_count = "Quantidade de erros",
        quality_warning_count = "Quantidade de avisos",
        quality_error_message = "Mensagem de erro:",
        results_no_rows = "Nenhuma linha de consulta disponivel.",
        nodes_no_details = "Nenhum detalhe de no disponivel para exportacoes do editor SQL.",
        connections_no_details = "Nenhum detalhe de conexao disponivel para exportacoes do editor SQL.",
        theme_light = "Claro",
        theme_dark = "Escuro",
        footer_generated_by = "Gerado por",
      };
    }

    return new
    {
      theme_toggle = "Toggle theme",
      keyboard_shortcuts_aria = "Keyboard shortcuts",
      shortcuts_title = "Shortcuts",
      shortcuts_focus_search = "Focus active search",
      shortcuts_results_filter = "Results filter",
      shortcuts_metadata_filter = "Metadata filter",
      shortcuts_nodes_filter = "Nodes filter",
      shortcuts_connections_filter = "Connections filter",
      section_execution_summary = "Execution Summary",
      section_validation_summary = "Validation Summary",
      section_query_results = "Query Results",
      section_node_inventory = "Node Inventory",
      section_connection_map = "Connection Map",
      section_sql_query = "SQL Query",
      section_optional_metadata = "Optional Metadata",
      badge_primary_output = "Primary Output",
      button_copy_sql = "Copy SQL",
      button_expand = "Expand",
      button_collapse = "Collapse",
      filter_results_placeholder = "Filter results",
      filter_nodes_placeholder = "Filter nodes",
      filter_connections_placeholder = "Filter connections",
      filter_metadata_placeholder = "Filter metadata",
      subpanel_filters = "Filters",
      subpanel_orders = "Orders",
      subpanel_columns = "Columns",
      filter_operator_contains = "Contains",
      filter_operator_equals = "= Equals",
      filter_operator_not_equal = "!= Not equal",
      filter_operator_greater_than = "> Greater than",
      filter_operator_less_than = "< Less than",
      filter_operator_greater_or_equal = ">= Greater or equal",
      filter_operator_less_or_equal = "<= Less or equal",
      filter_value_placeholder = "Filter value",
      filter_select_date = "Select a date",
      filter_add = "Add Filter",
      filter_clear = "Clear Filters",
      order_direction_asc = "Ascending",
      order_direction_desc = "Descending",
      order_apply = "Apply Order",
      order_reset = "Reset Order",
      columns_show_all = "Show All",
      columns_hide_all = "Hide All",
      columns_reset = "Reset Columns",
      table_columns = "Columns",
      table_select_one_column = "Select at least one column.",
      table_no_rows = "No rows available.",
      pager_showing_of = "Showing {0}-{1} of {2}",
      pager_prev = "Prev",
      pager_next = "Next",
      filters_none_active = "No filters active.",
      filters_metadata_none_active = "No metadata filters active.",
      button_remove = "Remove",
      columns_none_available = "No columns available.",
      field_category = "Category",
      field_type = "Type",
      field_title = "Title",
      field_status = "Status",
      field_from_node = "From Node",
      field_from_pin = "From Pin",
      field_to_node = "To Node",
      field_to_pin = "To Pin",
      field_data_type = "Data Type",
      field_field = "Field",
      field_label = "Label",
      field_value = "Value",
      summary_status = "Status",
      summary_hint_execution_outcome = "Execution outcome",
      summary_execution_time = "Execution Time",
      summary_hint_measured_duration = "Measured duration",
      summary_rows = "Rows",
      summary_hint_returned_rows = "Returned rows",
      summary_columns = "Columns",
      summary_hint_detected_columns = "Detected output columns",
      summary_error_prefix = "Error:",
      sql_not_available = "-- SQL not available.",
      quality_generated_errors = "Generated with errors",
      quality_generated_warnings = "Generated with warnings",
      quality_generated_success = "Query generated successfully",
      quality_status = "Status",
      quality_error_count = "Error Count",
      quality_warning_count = "Warning Count",
      quality_error_message = "Error Message:",
      results_no_rows = "No query rows available.",
      nodes_no_details = "No node details available for SQL editor exports.",
      connections_no_details = "No connection details available for SQL editor exports.",
      theme_light = "Light",
      theme_dark = "Dark",
      footer_generated_by = "Generated by",
    };
  }

    private static object BuildJsonPayload(SqlEditorReportExportContext context, SqlEditorReportExportRequest request)
    {
        string generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        string generatedAtIso = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
        object meta = BuildMeta(context, request, generatedAt, generatedAtIso);
        object result = BuildExecutionResultObject(context.ExecutionResult);
        bool includeGraphSections = request.IncludeMetadata && request.IncludeNodeDetails;
        object[]? nodes = includeGraphSections ? [] : null;
        object[]? connections = includeGraphSections ? [] : null;
        bool hasSql = !string.IsNullOrWhiteSpace(context.Sql);

        return new
        {
            version = ReportVersion,
            meta,
            sql = context.Sql,
            hasSql,
            result,
            schema = request.IncludeSchema ? context.SchemaColumns : [],
          rows = context.ResultRows,
            nodes,
            connections,
        };
    }

    private static object BuildMeta(
        SqlEditorReportExportContext context,
        SqlEditorReportExportRequest request,
        string generatedAt,
        string generatedAtIso)
    {
        string normalizedStatus = NormalizeStatus(context.ExecutionResult.Status);
        int errorCount = normalizedStatus == "error" ? 1 : 0;
        int warningCount = normalizedStatus == "warning" ? 1 : 0;
        int columnCount = context.SchemaColumns.Count;
        int joinCount = CountMatches(context.Sql, @"\bjoin\b");
        int aggregateCount = CountMatches(context.Sql, @"\b(count|sum|avg|min|max|string_agg|group_concat)\s*\(");
        bool hasSubquery = ContainsSubquery(context.Sql);
        string title = string.IsNullOrWhiteSpace(request.Title) ? context.TabTitle : request.Title;
        string? description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        ConnectionConfig? connection = context.Connection;
        object[] metadataRows = request.IncludeMetadata
          ?
          [
            new { field = "provider", label = "Provider", value = connection?.Provider.ToString(), kind = "text" },
            new { field = "database", label = "Database", value = connection?.Database, kind = "text" },
            new { field = "host", label = "Host", value = connection?.Host, kind = "text" },
            new { field = "dialect", label = "Dialect", value = InferDialect(connection?.Provider), kind = "text" },
            new { field = "executionDate", label = "Execution Date", value = generatedAtIso, kind = "date" },
            new { field = "locale", label = "Locale", value = CultureInfo.CurrentCulture.Name, kind = "text" },
            new { field = "timezone", label = "Timezone", value = TimeZoneInfo.Local.Id, kind = "text" },
            new { field = "engineVersion", label = "Engine Version", value = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "—", kind = "text" },
            new { field = "filePath", label = "File Path", value = context.ActiveFilePath, kind = "text" },
            new { field = "columnCount", label = "Columns", value = columnCount, kind = "number" },
            new { field = "joinCount", label = "Join Count", value = joinCount, kind = "number" },
            new { field = "aggregateCount", label = "Aggregate Count", value = aggregateCount, kind = "number" },
            new { field = "hasSubquery", label = "Has Subquery", value = hasSubquery, kind = "bool" },
            new { field = "warningCount", label = "Warnings", value = warningCount, kind = "number" },
            new { field = "errorCount", label = "Errors", value = errorCount, kind = "number" },
          ]
          : [];

        object summary = new
        {
          status = normalizedStatus,
          success = normalizedStatus == "success",
          executedAt = generatedAt,
          executionTimeMs = context.ExecutionResult.ExecutionTimeMs,
          rowCount = context.ExecutionResult.RowCount,
          sqlLength = context.Sql.Length,
          errorMessage = context.ExecutionResult.ErrorMessage,
        };

        return new
        {
            reportVersion = ReportVersion,
          includeMetadata = request.IncludeMetadata,
          useDashForEmptyFields = request.UseDashForEmptyFields,
            title,
            description,
            generatedAt,
            generatedAtIso,
          summary,
          metadata = metadataRows,
          provider = connection?.Provider.ToString() ?? "—",
          providerVersion = "—",
          database = connection?.Database ?? "—",
          host = connection?.Host ?? "—",
          dialect = InferDialect(connection?.Provider),
          engineVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "—",
          timezone = TimeZoneInfo.Local.Id,
          locale = CultureInfo.CurrentCulture.Name,
          nodeCount = 0,
          connCount = 0,
          columnCount,
          errorCount,
          warningCount,
          orphanCount = 0,
          namingConformance = "—",
          joinCount,
          aggregateCount,
          hasSubquery,
          queryLength = context.Sql.Length,
          rowCount = context.ExecutionResult.RowCount,
          executionTimeMs = context.ExecutionResult.ExecutionTimeMs,
          status = normalizedStatus,
          filePath = context.ActiveFilePath ?? "—",
        };
    }

    private static object BuildExecutionResultObject(SqlEditorReportExecutionResult executionResult)
    {
        return new
        {
            rowCount = executionResult.RowCount,
            executionTimeMs = executionResult.ExecutionTimeMs,
          status = NormalizeStatus(executionResult.Status),
          success = NormalizeStatus(executionResult.Status) == "success",
            errorMessage = executionResult.ErrorMessage,
        };
    }

    private static string InferDialect(DatabaseProvider? provider)
    {
        return provider switch
        {
            DatabaseProvider.Postgres => "postgresql",
            DatabaseProvider.SqlServer => "sqlserver",
            DatabaseProvider.MySql => "mysql",
            DatabaseProvider.SQLite => "sqlite",
            _ => "unknown",
        };
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return "success";

        string normalized = status.Trim().ToLowerInvariant();
        return normalized switch
        {
            "error" => "error",
            "warning" => "warning",
            _ => "success",
        };
    }

    private static int CountMatches(string input, string pattern)
    {
        if (string.IsNullOrWhiteSpace(input))
            return 0;

        return Regex.Matches(input, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count;
    }

    private static bool ContainsSubquery(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return false;

        return Regex.IsMatch(sql, @"\(\s*select\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
               || Regex.IsMatch(sql, @"\bexists\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string SerializeForInlineJs(object value)
    {
        return JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    private static string EscapeJsTemplateLiteral(string value)
    {
      string escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("$", "\\$", StringComparison.Ordinal);

      // Prevent accidental script tag termination when SQL contains </script>
      return Regex.Replace(escaped, "</script", "<\\/script", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string HtmlEncode(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
    }
}

using System.Text;

namespace AkkornStudio.UI.Services.SqlEditor.Reports;

internal static class SqlEditorReportHtmlSections
{
    public static string BuildResultsSection()
    {
        return """
  <section id="s-results">
    <div class="section-header">
      <h2><span class="section-icon" aria-hidden="true"><svg viewBox="0 0 16 16" focusable="false"><path d="M2 2h12v3H2V2Zm0 4h12v3H2V6Zm0 4h12v3H2v-3Z"/></svg></span><span data-i18n="section_query_results">Query Results</span></h2>
      <div class="controls">
        <input id="results-search" class="table-input" type="search" placeholder="Filter results" data-i18n-placeholder="filter_results_placeholder"/>
        <select id="results-page-size" class="table-select">
          <option value="10">10</option>
          <option value="25">25</option>
          <option value="50">50</option>
        </select>
        <button class="collapse-btn" data-collapse-target="s-results" type="button">▾ Collapse</button>
      </div>
    </div>
    <div class="section-body">
"""
        + BuildFilterAndOrderPanels("results")
        + """
      <div id="results-body"></div>
    </div>
  </section>
""";
    }

    public static string BuildFilterAndOrderPanels(string prefix)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"      <div class=\"subpanel\" id=\"{prefix}-filters-panel\">");
        sb.AppendLine("        <div class=\"subpanel-header\">");
        sb.AppendLine("          <div class=\"subpanel-title\" data-i18n=\"subpanel_filters\">Filters</div>");
        sb.AppendLine($"          <button class=\"tool-toggle\" type=\"button\" data-subcollapse-target=\"{prefix}-filters-panel\" aria-expanded=\"true\">▾ Collapse</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"subpanel-body\">");
        sb.AppendLine("        <div class=\"filter-toolbar\">");
        sb.AppendLine($"          <select id=\"{prefix}-filter-col\" class=\"table-select\"></select>");
        sb.AppendLine($"          <select id=\"{prefix}-filter-op\" class=\"table-select\">");
        sb.AppendLine("            <option value=\"contains\" data-i18n=\"filter_operator_contains\">Contains</option>");
        sb.AppendLine("            <option value=\"=\" data-i18n=\"filter_operator_equals\">= Equals</option>");
        sb.AppendLine("            <option value=\"!=\" data-i18n=\"filter_operator_not_equal\">!= Not equal</option>");
        sb.AppendLine("            <option value=\">\" data-i18n=\"filter_operator_greater_than\">&gt; Greater than</option>");
        sb.AppendLine("            <option value=\"<\" data-i18n=\"filter_operator_less_than\">&lt; Less than</option>");
        sb.AppendLine("            <option value=\">=\" data-i18n=\"filter_operator_greater_or_equal\">&gt;= Greater or equal</option>");
        sb.AppendLine("            <option value=\"<=\" data-i18n=\"filter_operator_less_or_equal\">&lt;= Less or equal</option>");
        sb.AppendLine("          </select>");
        sb.AppendLine($"          <input id=\"{prefix}-filter-val\" class=\"table-input\" type=\"text\" placeholder=\"Filter value\" data-i18n-placeholder=\"filter_value_placeholder\"/>");
        sb.AppendLine($"          <button id=\"{prefix}-add-filter\" class=\"collapse-btn\" type=\"button\" data-i18n=\"filter_add\">Add Filter</button>");
        sb.AppendLine($"          <button id=\"{prefix}-clear-filters\" class=\"collapse-btn\" type=\"button\" data-i18n=\"filter_clear\">Clear Filters</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine($"        <div class=\"filter-list\" id=\"{prefix}-filter-list\"></div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");

        sb.AppendLine($"      <div class=\"subpanel\" id=\"{prefix}-order-panel\">");
        sb.AppendLine("        <div class=\"subpanel-header\">");
        sb.AppendLine("          <div class=\"subpanel-title\" data-i18n=\"subpanel_orders\">Orders</div>");
        sb.AppendLine($"          <button class=\"tool-toggle\" type=\"button\" data-subcollapse-target=\"{prefix}-order-panel\" aria-expanded=\"true\">▾ Collapse</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"subpanel-body\">");
        sb.AppendLine("        <div class=\"filter-toolbar\">");
        sb.AppendLine($"          <select id=\"{prefix}-order-col\" class=\"table-select\"></select>");
        sb.AppendLine($"          <select id=\"{prefix}-order-dir\" class=\"table-select\">");
        sb.AppendLine("            <option value=\"asc\" data-i18n=\"order_direction_asc\">Ascending</option>");
        sb.AppendLine("            <option value=\"desc\" data-i18n=\"order_direction_desc\">Descending</option>");
        sb.AppendLine("          </select>");
        sb.AppendLine($"          <button id=\"{prefix}-apply-order\" class=\"collapse-btn\" type=\"button\" data-i18n=\"order_apply\">Apply Order</button>");
        sb.AppendLine($"          <button id=\"{prefix}-reset-order\" class=\"collapse-btn\" type=\"button\" data-i18n=\"order_reset\">Reset Order</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");

        sb.AppendLine($"      <div class=\"subpanel\" id=\"{prefix}-columns-panel\">");
        sb.AppendLine("        <div class=\"subpanel-header\">");
        sb.AppendLine("          <div class=\"subpanel-title\" data-i18n=\"subpanel_columns\">Columns</div>");
        sb.AppendLine($"          <button class=\"tool-toggle\" type=\"button\" data-subcollapse-target=\"{prefix}-columns-panel\" aria-expanded=\"true\">▾ Collapse</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"subpanel-body\">");
        sb.AppendLine($"          <div class=\"filter-list\" id=\"{prefix}-column-list\"></div>");
        sb.AppendLine("          <div class=\"filter-toolbar\">");
        sb.AppendLine($"            <button id=\"{prefix}-columns-all\" class=\"collapse-btn\" type=\"button\" data-i18n=\"columns_show_all\">Show All</button>");
        sb.AppendLine($"            <button id=\"{prefix}-columns-none\" class=\"collapse-btn\" type=\"button\" data-i18n=\"columns_hide_all\">Hide All</button>");
        sb.AppendLine($"            <button id=\"{prefix}-columns-reset\" class=\"collapse-btn\" type=\"button\" data-i18n=\"columns_reset\">Reset Columns</button>");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");

        return sb.ToString();
    }
}

using AkkornStudio.Core;
using AkkornStudio.Metadata;

namespace AkkornStudio.UI.Services.SqlEditor;

public sealed class SqlHoverDocumentationService
{
    private static readonly SqlSymbolTableBuilder SymbolTableBuilder = new();

    public HoverDocumentationInfo? TryResolve(
        string fullText,
        int caretOffset,
        DbMetadata? metadata,
        DatabaseProvider provider)
    {
        ArgumentNullException.ThrowIfNull(fullText);
        if (metadata is null || caretOffset < 0 || caretOffset > fullText.Length)
            return null;

        string token = ReadIdentifierToken(fullText, caretOffset);
        if (string.IsNullOrWhiteSpace(token))
            return null;

        HoverDocumentationInfo? tableDoc = TryResolveTableDocumentation(metadata, token);
        if (tableDoc is not null)
            return tableDoc;

        return TryResolveColumnDocumentation(fullText, caretOffset, metadata, provider, token);
    }

    private static HoverDocumentationInfo? TryResolveTableDocumentation(DbMetadata metadata, string token)
    {
        TableMetadata? table = ResolveTable(metadata, token);
        if (table is null)
            return null;

        return new HoverDocumentationInfo(
            $"{table.FullName} [{table.Kind}] - colunas: {table.Columns.Count}");
    }

    private static HoverDocumentationInfo? TryResolveColumnDocumentation(
        string fullText,
        int caretOffset,
        DbMetadata metadata,
        DatabaseProvider provider,
        string token)
    {
        string qualifier;
        string columnName;

        int dot = token.LastIndexOf('.');
        if (dot > 0 && dot < token.Length - 1)
        {
            qualifier = token[..dot];
            columnName = token[(dot + 1)..];
        }
        else
        {
            qualifier = string.Empty;
            columnName = token;
        }

        if (string.IsNullOrWhiteSpace(columnName))
            return null;

        SqlSymbolTable symbols = SymbolTableBuilder.Build(fullText[..caretOffset], provider);

        if (!string.IsNullOrWhiteSpace(qualifier))
        {
            string tableRef = symbols.TryResolveBinding(qualifier, out SqlTableBindingSymbol? binding) && binding is not null
                ? binding.TableRef
                : qualifier;
            TableMetadata? table = ResolveTable(metadata, tableRef);
            if (table is null)
                return null;

            ColumnMetadata? column = table.Columns.FirstOrDefault(c =>
                c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
            return column is null ? null : BuildColumnDocumentation(table, column);
        }

        var matches = new List<(TableMetadata Table, ColumnMetadata Column)>();
        foreach (TableMetadata table in metadata.AllTables)
        {
            ColumnMetadata? column = table.Columns.FirstOrDefault(c =>
                c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
            if (column is not null)
                matches.Add((table, column));
        }

        if (matches.Count != 1)
            return null;

        return BuildColumnDocumentation(matches[0].Table, matches[0].Column);
    }

    private static HoverDocumentationInfo BuildColumnDocumentation(TableMetadata table, ColumnMetadata column)
    {
        string nullable = column.IsNullable ? "NULL" : "NOT NULL";
        string keyFlag = column.IsPrimaryKey ? " PK" : column.IsForeignKey ? " FK" : string.Empty;
        return new HoverDocumentationInfo(
            $"{table.FullName}.{column.Name}: {column.DataType} {nullable}{keyFlag}".Trim());
    }

    private static TableMetadata? ResolveTable(DbMetadata metadata, string tableRef)
    {
        string normalized = tableRef.Trim();
        return metadata.AllTables.FirstOrDefault(t =>
                   t.FullName.Equals(normalized, StringComparison.OrdinalIgnoreCase))
               ?? metadata.AllTables.FirstOrDefault(t =>
                   t.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string ReadIdentifierToken(string fullText, int caretOffset)
    {
        if (fullText.Length == 0)
            return string.Empty;

        int index = Math.Clamp(caretOffset, 0, fullText.Length - 1);
        if (!IsIdentifierChar(fullText[index]))
            index = Math.Max(0, index - 1);

        if (!IsIdentifierChar(fullText[index]))
            return string.Empty;

        int start = index;
        while (start > 0 && IsIdentifierChar(fullText[start - 1]))
            start--;

        int end = index;
        while (end + 1 < fullText.Length && IsIdentifierChar(fullText[end + 1]))
            end++;

        return fullText[start..(end + 1)];
    }

    private static bool IsIdentifierChar(char c)
    {
        return char.IsLetterOrDigit(c) || c is '_' or '.';
    }
}

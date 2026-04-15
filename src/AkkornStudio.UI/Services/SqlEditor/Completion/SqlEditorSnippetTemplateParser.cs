using System.Text;

namespace AkkornStudio.UI.Services.SqlEditor;

public static class SqlEditorSnippetTemplateParser
{
    public static SqlEditorSnippetTemplate Parse(string insertText)
    {
        if (string.IsNullOrEmpty(insertText))
            return new SqlEditorSnippetTemplate(string.Empty, []);

        var output = new StringBuilder(insertText.Length);
        var stops = new List<(int Number, int Offset)>();

        int i = 0;
        while (i < insertText.Length)
        {
            char c = insertText[i];
            if (c != '$' || i + 1 >= insertText.Length || !char.IsDigit(insertText[i + 1]))
            {
                output.Append(c);
                i++;
                continue;
            }

            int numberStart = i + 1;
            int numberEnd = numberStart;
            while (numberEnd < insertText.Length && char.IsDigit(insertText[numberEnd]))
                numberEnd++;

            string raw = insertText[numberStart..numberEnd];
            if (!int.TryParse(raw, out int stopNumber))
            {
                output.Append(c);
                i++;
                continue;
            }

            stops.Add((stopNumber, output.Length));
            i = numberEnd;
        }

        IReadOnlyList<int> orderedOffsets = stops
            .OrderBy(static stop => stop.Number == 0 ? int.MaxValue : stop.Number)
            .ThenBy(static stop => stop.Offset)
            .Select(static stop => stop.Offset)
            .ToList();

        return new SqlEditorSnippetTemplate(output.ToString(), orderedOffsets);
    }
}

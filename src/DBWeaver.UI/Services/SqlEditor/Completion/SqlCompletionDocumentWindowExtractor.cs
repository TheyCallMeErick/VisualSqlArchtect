namespace DBWeaver.UI.Services.SqlEditor;

public sealed class SqlCompletionDocumentWindowExtractor
{
    public SqlCompletionDocumentWindow Extract(string fullText, int caretOffset)
    {
        ArgumentNullException.ThrowIfNull(fullText);
        if (caretOffset < 0 || caretOffset > fullText.Length)
            throw new ArgumentOutOfRangeException(nameof(caretOffset));

        int start = 0;
        bool inLineComment = false;
        bool inBlockComment = false;
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        bool inBracketQuote = false;
        bool inBacktickQuote = false;

        for (int i = 0; i < caretOffset; i++)
        {
            char current = fullText[i];
            char next = i + 1 < caretOffset ? fullText[i + 1] : '\0';

            if (inLineComment)
            {
                if (current is '\r' or '\n')
                    inLineComment = false;

                continue;
            }

            if (inBlockComment)
            {
                if (current == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                }

                continue;
            }

            if (inSingleQuote)
            {
                if (current == '\'' && next == '\'')
                {
                    i++;
                    continue;
                }

                if (current == '\'')
                    inSingleQuote = false;

                continue;
            }

            if (inDoubleQuote)
            {
                if (current == '"')
                    inDoubleQuote = false;

                continue;
            }

            if (inBracketQuote)
            {
                if (current == ']')
                    inBracketQuote = false;

                continue;
            }

            if (inBacktickQuote)
            {
                if (current == '`')
                    inBacktickQuote = false;

                continue;
            }

            if (current == '-' && next == '-')
            {
                inLineComment = true;
                i++;
                continue;
            }

            if (current == '/' && next == '*')
            {
                inBlockComment = true;
                i++;
                continue;
            }

            if (current == '\'')
            {
                inSingleQuote = true;
                continue;
            }

            if (current == '"')
            {
                inDoubleQuote = true;
                continue;
            }

            if (current == '[')
            {
                inBracketQuote = true;
                continue;
            }

            if (current == '`')
            {
                inBacktickQuote = true;
                continue;
            }

            if (current == ';')
                start = i + 1;
        }

        int length = Math.Max(0, caretOffset - start);
        string window = length == 0 ? string.Empty : fullText[start..caretOffset];
        return new SqlCompletionDocumentWindow(start, caretOffset, window);
    }
}

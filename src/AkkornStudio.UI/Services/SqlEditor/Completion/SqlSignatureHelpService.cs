using AkkornStudio.Core;
using System.Text;

namespace AkkornStudio.UI.Services.SqlEditor;

public sealed class SqlSignatureHelpService
{
    private const int MaxBackwardScanChars = 2048;
    private readonly FunctionSignatureRegistry _registry;

    public SqlSignatureHelpService(FunctionSignatureRegistry? registry = null)
    {
        _registry = registry ?? new FunctionSignatureRegistry();
    }

    public SignatureHelpInfo? TryResolve(string fullText, int caretOffset, DatabaseProvider provider)
    {
        ArgumentNullException.ThrowIfNull(fullText);
        if (caretOffset < 0 || caretOffset > fullText.Length)
            return null;

        if (!TryFindCallOpenParen(fullText, caretOffset, out int openParenOffset))
            return null;

        string? functionName = TryReadFunctionName(fullText, openParenOffset);
        if (string.IsNullOrWhiteSpace(functionName))
            return null;

        FunctionSignature? signature = _registry.TryResolve(provider, functionName);
        if (signature is null)
            return null;

        int activeParameter = ResolveActiveParameterIndex(fullText, openParenOffset, caretOffset);
        string displayText = BuildDisplayText(signature, activeParameter);
        return new SignatureHelpInfo(signature, activeParameter, displayText);
    }

    private static bool TryFindCallOpenParen(string text, int caretOffset, out int openParenOffset)
    {
        int minOffset = Math.Max(0, caretOffset - MaxBackwardScanChars);
        int depth = 0;
        for (int i = caretOffset - 1; i >= minOffset; i--)
        {
            char c = text[i];
            if (c == ')')
            {
                depth++;
                continue;
            }

            if (c != '(')
                continue;

            if (depth == 0)
            {
                openParenOffset = i;
                return true;
            }

            depth--;
        }

        openParenOffset = -1;
        return false;
    }

    private static string? TryReadFunctionName(string text, int openParenOffset)
    {
        int end = openParenOffset - 1;
        while (end >= 0 && char.IsWhiteSpace(text[end]))
            end--;

        if (end < 0)
            return null;

        int start = end;
        while (start >= 0 && (char.IsLetterOrDigit(text[start]) || text[start] == '_'))
            start--;

        start++;
        if (start > end)
            return null;

        return text[start..(end + 1)];
    }

    private static int ResolveActiveParameterIndex(string text, int openParenOffset, int caretOffset)
    {
        int commas = 0;
        int nestedDepth = 0;
        for (int i = openParenOffset + 1; i < caretOffset; i++)
        {
            char c = text[i];
            if (c == '(')
            {
                nestedDepth++;
                continue;
            }

            if (c == ')')
            {
                if (nestedDepth > 0)
                    nestedDepth--;
                continue;
            }

            if (c == ',' && nestedDepth == 0)
                commas++;
        }

        return Math.Max(0, commas);
    }

    private static string BuildDisplayText(FunctionSignature signature, int activeParameterIndex)
    {
        if (signature.Parameters.Count == 0)
            return $"{signature.Name}() -> {signature.ReturnType}";

        int highlighted = Math.Clamp(activeParameterIndex, 0, signature.Parameters.Count - 1);
        var builder = new StringBuilder();
        builder.Append(signature.Name);
        builder.Append('(');
        for (int i = 0; i < signature.Parameters.Count; i++)
        {
            if (i > 0)
                builder.Append(", ");

            FunctionParameterSignature parameter = signature.Parameters[i];
            string segment = $"{parameter.Name}: {parameter.Type}";
            if (i == highlighted)
                builder.Append('[').Append(segment).Append(']');
            else
                builder.Append(segment);
        }

        builder.Append(") -> ");
        builder.Append(signature.ReturnType);
        return builder.ToString();
    }
}
